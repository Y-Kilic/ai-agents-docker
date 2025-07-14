namespace Orchestrator.API.Data;

using System.Collections.Concurrent;
using Shared.Models;

public class InMemoryAgentRepository : IAgentRepository
{
    private readonly ConcurrentDictionary<string, AgentInfo> _agents = new();

    public void Add(AgentInfo agent) => _agents[agent.Id] = agent;

    public void Remove(string id) => _agents.TryRemove(id, out _);

    public IEnumerable<AgentInfo> GetAll() => _agents.Values;

    public AgentInfo? Get(string id)
    {
        _agents.TryGetValue(id, out var info);
        return info;
    }
}
