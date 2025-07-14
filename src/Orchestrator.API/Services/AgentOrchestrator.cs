using Docker.DotNet;
using Docker.DotNet.Models;
using Shared.Models;
using System.Collections.Concurrent;

namespace Orchestrator.API.Services;

public class AgentOrchestrator
{
    private readonly DockerClient _docker;
    private const string ImageName = "worldseed-agent";
    private readonly ConcurrentDictionary<string, AgentInfo> _agents = new();

    public AgentOrchestrator()
    {
        _docker = new DockerClientConfiguration().CreateClient();
    }

    public async Task<string> StartAgentAsync(string goal, AgentType type = AgentType.Default)
    {
        // ensure image exists
        await EnsureImageAsync();

        var env = new List<string> { $"GOAL={goal}" };
        if (!AgentProfiles.TryGetProfile(type, out var config))
            config = new AgentConfig("agent", type);

        var container = await _docker.Containers.CreateContainerAsync(new CreateContainerParameters
        {
            Image = ImageName,
            Env = env,
            HostConfig = new HostConfig
            {
                AutoRemove = true
            }
        });

        await _docker.Containers.StartContainerAsync(container.ID, null);

        var info = new AgentInfo(container.ID, type);
        _agents[container.ID] = info;

        return container.ID;
    }

    public IEnumerable<AgentInfo> ListAgents() => _agents.Values;

    public async Task StopAgentAsync(string id)
    {
        if (_agents.TryRemove(id, out _))
        {
            await _docker.Containers.StopContainerAsync(id, new ContainerStopParameters());
        }
    }

    private async Task EnsureImageAsync()
    {
        var images = await _docker.Images.ListImagesAsync(new ImagesListParameters());
        if (images.Any(i => i.RepoTags != null && i.RepoTags.Contains(ImageName)))
            return;

        throw new InvalidOperationException($"Docker image '{ImageName}' not found. Build it before starting agents.");
    }
}
