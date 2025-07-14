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
    private readonly Data.IUnitOfWork _uow;
    private readonly string _orchestratorUrl;

    public AgentOrchestrator(Data.IUnitOfWork uow)
    {
        _uow = uow;
        _useLocal = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("USE_LOCAL_AGENT"));
        _orchestratorUrl = Environment.GetEnvironmentVariable("ORCHESTRATOR_URL") ?? "http://localhost:5000";
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

    public async Task<string> StartAgentAsync(string goal, AgentType type = AgentType.Default)
    {
        var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");

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
                RedirectStandardOutput = false,
                RedirectStandardError = false,
            };
            if (!string.IsNullOrWhiteSpace(apiKey))
            {
                psi.Environment["OPENAI_API_KEY"] = apiKey;
            }

            psi.Environment["AGENT_ID"] = id;
            psi.Environment["ORCHESTRATOR_URL"] = _orchestratorUrl;

            var proc = Process.Start(psi) ?? throw new InvalidOperationException("Failed to start local agent process");
            proc.EnableRaisingEvents = true;
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
            $"ORCHESTRATOR_URL={_orchestratorUrl}"
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
            AutoRemove = true,
            Memory = 256 * 1024 * 1024, // 256MB limit
            NanoCPUs = 1_000_000_000,   // 1 CPU
            NetworkMode = "bridge",
            SecurityOpt = new List<string>
            {
                "apparmor=worldseed-agent",
                $"seccomp={Path.GetFullPath("docker/profiles/seccomp-agent.json")}"
            },
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
        if (images.Any(i => i.RepoTags != null && i.RepoTags.Contains(ImageName)))
            return;

        throw new InvalidOperationException($"Docker image '{ImageName}' not found. Build it before starting agents.");
    }
}
