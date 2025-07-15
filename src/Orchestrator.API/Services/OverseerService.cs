using Shared.Models;
using System.Collections.Concurrent;

namespace Orchestrator.API.Services;

public class OverseerService
{
    private readonly AgentOrchestrator _agents;
    private readonly ConcurrentDictionary<string, OverseerInfo> _overseers = new();

    public OverseerService(AgentOrchestrator agents)
    {
        _agents = agents;
    }

    public async Task<string> StartAsync(string goal, int loops = 5)
    {
        var id = Guid.NewGuid().ToString("N");
        var subgoals = DecomposeGoal(goal);
        var agentIds = new List<string>();
        foreach (var g in subgoals)
        {
            var agentId = await _agents.StartAgentAsync(g, AgentType.Default, loops);
            agentIds.Add(agentId);
        }
        _overseers[id] = new OverseerInfo(id, goal, agentIds);
        return id;
    }

    public IEnumerable<OverseerInfo> List() => _overseers.Values;

    public async Task<OverseerStatus?> GetStatusAsync(string id)
    {
        if (!_overseers.TryGetValue(id, out var info))
            return null;
        var logs = new Dictionary<string, List<string>>();
        foreach (var agentId in info.AgentIds)
        {
            var messages = await _agents.GetAllMessagesAsync(agentId);
            logs[agentId] = messages;
        }
        return new OverseerStatus(info, logs);
    }

    public async Task StopAsync(string id)
    {
        if (!_overseers.TryRemove(id, out var info))
            return;
        foreach (var agentId in info.AgentIds)
            await _agents.StopAgentAsync(agentId);
    }

    private static List<string> DecomposeGoal(string goal)
    {
        var parts = goal.Split(new[] { '.', '\n', ';' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(p => p.Trim())
            .Where(p => !string.IsNullOrEmpty(p))
            .ToList();
        if (parts.Count <= 1)
            parts = new List<string> { goal };
        return parts;
    }
}
