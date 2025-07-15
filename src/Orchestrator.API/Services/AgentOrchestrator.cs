using Docker.DotNet;
using Docker.DotNet.Models;
using Shared.Models;
using Orchestrator.API.Data;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace Orchestrator.API.Services;

public class AgentOrchestrator
{
    private readonly DockerClient? _docker;
    private readonly bool _useLocal;
    private const string ImageName = "worldseed-agent";
    private readonly ConcurrentDictionary<string, Process> _processes = new();
    private readonly ConcurrentDictionary<string, string> _containers = new();
    private readonly ConcurrentDictionary<string, List<string>> _localLogs = new();
    private readonly ConcurrentDictionary<string, int> _logOffsets = new();
    private readonly Data.IUnitOfWork _uow;
    private readonly string _orchestratorUrl;
    private bool _useOpenAI;
    private string? _apiKey;

    public AgentOrchestrator(Data.IUnitOfWork uow)
    {
        _uow = uow;
        _useLocal = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("USE_LOCAL_AGENT"));
        _apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        _useOpenAI = !string.IsNullOrWhiteSpace(_apiKey);
        // Agents always communicate with the API over http://localhost:5000.
        // Containers inherit this value through the ORCHESTRATOR_URL environment
        // variable so logs and memory are posted back to the host API.
        _orchestratorUrl = Environment.GetEnvironmentVariable("ORCHESTRATOR_URL")
            ?? "http://localhost:5000";
        if (!_useLocal)
        {
            try
            {
                _docker = new DockerClientConfiguration().CreateClient();
                // ping to verify connection
                _docker.System.PingAsync().GetAwaiter().GetResult();
            }
            catch
            {
                _useLocal = true;
            }
        }
    }

    public LLMConfig GetLLMConfig() => new(_useOpenAI, _apiKey);

    public void SetLLMConfig(LLMConfig config)
    {
        _useOpenAI = config.UseOpenAI;
        _apiKey = config.ApiKey;
    }

    public async Task<string> StartAgentAsync(string goal, AgentType type = AgentType.Default, int loops = 3)
    {
        var apiKey = _useOpenAI ? _apiKey : null;

        var id = Guid.NewGuid().ToString("N");

        if (_useLocal)
        {
            var runtimeDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../src/Agent.Runtime"));
            if (!Directory.Exists(runtimeDir))
            {
                runtimeDir = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "src", "Agent.Runtime"));
            }

            var projectPath = Path.Combine(runtimeDir, "Agent.Runtime.csproj");
            var psi = new ProcessStartInfo("dotnet", $"run --project {projectPath} -- {goal}")
            {
                WorkingDirectory = runtimeDir,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            if (!string.IsNullOrWhiteSpace(apiKey))
            {
                psi.Environment["OPENAI_API_KEY"] = apiKey;
            }

            psi.Environment["AGENT_ID"] = id;
            psi.Environment["ORCHESTRATOR_URL"] = _orchestratorUrl;
            psi.Environment["LOOP_COUNT"] = loops.ToString();

            var proc = new Process { StartInfo = psi, EnableRaisingEvents = true };
            var logList = new List<string>();
            _localLogs[id] = logList;
            _logOffsets[id] = 0;
            proc.OutputDataReceived += (s, e) => { if (e.Data != null) logList.Add(e.Data); };
            proc.ErrorDataReceived += (s, e) => { if (e.Data != null) logList.Add(e.Data); };
            proc.Exited += (s, e) =>
            {
                _processes.TryRemove(id, out _);
                _uow.Agents.Remove(id);
                proc.Dispose();
            };
            proc.Start();
            proc.BeginOutputReadLine();
            proc.BeginErrorReadLine();
            proc.Exited += (s, e) =>
            {
                _processes.TryRemove(id, out _);
                _uow.Agents.Remove(id);
                proc.Dispose();
            };
            _processes[id] = proc;
            _uow.Agents.Add(new AgentInfo(id, type));
            await _uow.SaveChangesAsync();
            return id;
        }

        // ensure image exists
        await EnsureImageAsync();

        var env = new List<string>
        {
            $"GOAL={goal}",
            $"AGENT_ID={id}",
            $"ORCHESTRATOR_URL={_orchestratorUrl}",
            $"LOOP_COUNT={loops}"
        };

        if (!string.IsNullOrWhiteSpace(apiKey))
            env.Add($"OPENAI_API_KEY={apiKey}");



        if (!AgentProfiles.TryGetProfile(type, out var config))
            config = new AgentConfig("agent", type);

        var volumeName = $"agent-{Guid.NewGuid():N}";
        await _docker!.Volumes.CreateAsync(new VolumesCreateParameters
        {
            Name = volumeName
        });

        var hostConfig = new HostConfig
        {
            AutoRemove = false,
            Memory = 256 * 1024 * 1024, // 256MB limit
            NanoCPUs = 1_000_000_000,   // 1 CPU
            NetworkMode = "bridge",
            Mounts = new List<Mount>
            {
                new()
                {
                    Type = "volume",
                    Source = volumeName,
                    Target = "/agent"
                }
            }
        };

        var container = await _docker.Containers.CreateContainerAsync(new CreateContainerParameters
        {
            Image = ImageName,
            Env = env,
            HostConfig = hostConfig
        });

        await _docker.Containers.StartContainerAsync(container.ID, null);

        _containers[id] = container.ID;
        _logOffsets[id] = 0;
        var info = new AgentInfo(id, type);
        _uow.Agents.Add(info);
        await _uow.SaveChangesAsync();

        return id;
    }

    public IEnumerable<AgentInfo> ListAgents() => _uow.Agents.GetAll();

    public async Task StopAgentAsync(string id)
    {
        if (_useLocal)
        {
            if (_processes.TryRemove(id, out var proc))
            {
                try
                {
                    proc.Kill(true);
                }
                catch { }
                proc.Dispose();
            }
            _uow.Agents.Remove(id);
            await _uow.SaveChangesAsync();
            return;
        }

        _uow.Agents.Remove(id);
        await _uow.SaveChangesAsync();
        if (_containers.TryRemove(id, out var containerId))
        {
            await _docker!.Containers.StopContainerAsync(containerId, new ContainerStopParameters());
        }
    }

    private async Task EnsureImageAsync()
    {
        if (_useLocal)
            return;

        var images = await _docker!.Images.ListImagesAsync(new ImagesListParameters());

        if (images.Any(i =>
            i.RepoTags != null &&
            i.RepoTags.Any(tag => tag.StartsWith($"{ImageName}:"))))
        {
            return;
        }

        throw new InvalidOperationException($"Docker image '{ImageName}' not found. Build it before starting agents.");
    }

    public async Task<List<string>> GetMessagesAsync(string id)
    {
        var lines = await GetNewLogLinesAsync(id);
        return lines.Where(l => !l.StartsWith("MEMORY:")).ToList();
    }

    public async Task<List<string>> GetAllMessagesAsync(string id)
    {
        var lines = await GetAllLogLinesAsync(id);
        return lines.Where(l => !l.StartsWith("MEMORY:")).ToList();
    }

    public async Task<List<string>> GetMemoryAsync(string id)
    {
        var lines = await GetNewLogLinesAsync(id);
        return lines.Where(l => l.StartsWith("MEMORY:")).Select(l => l.Substring(7).Trim()).ToList();
    }

    private async Task<List<string>> GetNewLogLinesAsync(string id)
    {
        if (_useLocal)
        {
            if (!_localLogs.TryGetValue(id, out var buf))
                return new List<string>();
            var offset = _logOffsets.GetOrAdd(id, 0);
            if (offset >= buf.Count)
                return new List<string>();
            var lines = buf.Skip(offset).ToList();
            _logOffsets[id] = offset + lines.Count;
            return lines;
        }

        if (!_containers.TryGetValue(id, out var containerId))
            return new List<string>();

        var psi = new ProcessStartInfo("docker", $"logs {containerId}")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        var proc = Process.Start(psi) ?? throw new InvalidOperationException("failed to fetch logs");
        var content = await proc.StandardOutput.ReadToEndAsync();
        proc.WaitForExit();
        var linesAll = content.Split('\n', StringSplitOptions.RemoveEmptyEntries).ToList();
        var off = _logOffsets.GetOrAdd(id, 0);
        if (off >= linesAll.Count)
            return new List<string>();
        var newLines = linesAll.Skip(off).ToList();
        _logOffsets[id] = off + newLines.Count;
        return newLines;
    }

    private async Task<List<string>> GetAllLogLinesAsync(string id)
    {
        if (_useLocal)
        {
            if (!_localLogs.TryGetValue(id, out var buf))
                return new List<string>();
            return buf.ToList();
        }

        if (!_containers.TryGetValue(id, out var containerId))
            return new List<string>();

        var psi = new ProcessStartInfo("docker", $"logs {containerId}")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        var proc = Process.Start(psi) ?? throw new InvalidOperationException("failed to fetch logs");
        var content = await proc.StandardOutput.ReadToEndAsync();
        proc.WaitForExit();
        return content.Split('\n', StringSplitOptions.RemoveEmptyEntries).ToList();
    }
}
