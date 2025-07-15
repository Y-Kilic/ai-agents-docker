using Shared.Models;
using System.Collections.Concurrent;

namespace Orchestrator.API.Services;

public class OverseerService
{
    private readonly AgentOrchestrator _agents;

    private class OverseerState
    {
        public string Id { get; }
        public string Goal { get; }
        public int Loops { get; }
        public List<string> AgentIds { get; } = new();
        public CancellationTokenSource Cancellation { get; } = new();

        public OverseerState(string id, string goal, int loops)
        {
            Id = id;
            Goal = goal;
            Loops = loops;
        }
    }

    private readonly ConcurrentDictionary<string, OverseerState> _overseers = new();

    public OverseerService(AgentOrchestrator agents)
    {
        _agents = agents;
    }

    public Task<string> StartAsync(string goal, int loops = 5)
    {
        var id = Guid.NewGuid().ToString("N");
        var subgoals = DecomposeGoal(goal);
        var state = new OverseerState(id, goal, loops);
        _overseers[id] = state;

        _ = Task.Run(() => RunAsync(state, subgoals));

        return Task.FromResult(id);
    }

    public IEnumerable<OverseerInfo> List() =>
        _overseers.Values.Select(o => new OverseerInfo(o.Id, o.Goal, o.AgentIds.ToList()));

    public async Task<OverseerStatus?> GetStatusAsync(string id)
    {
        if (!_overseers.TryGetValue(id, out var state))
            return null;

        var info = new OverseerInfo(state.Id, state.Goal, state.AgentIds.ToList());
        var logs = new Dictionary<string, List<string>>();
        foreach (var agentId in state.AgentIds)
        {
            var messages = await _agents.GetAllMessagesAsync(agentId);
            logs[agentId] = messages;
        }

        return new OverseerStatus(info, logs);
    }

    public async Task StopAsync(string id)
    {
        if (!_overseers.TryRemove(id, out var state))
            return;

        state.Cancellation.Cancel();

        foreach (var agentId in state.AgentIds)
            await _agents.StopAgentAsync(agentId);
    }

    private async Task RunAsync(OverseerState state, List<string> subgoals)
    {
        foreach (var goal in subgoals)
        {
            await HandleSubgoalAsync(state, goal);
        }
    }

    private async Task HandleSubgoalAsync(OverseerState state, string subgoal)
    {
        var attempt = 1;
        var currentGoal = subgoal;
        while (!state.Cancellation.IsCancellationRequested)
        {
            var agentId = await _agents.StartAgentAsync(currentGoal, AgentType.Default, state.Loops);
            state.AgentIds.Add(agentId);

            var completed = await WaitForCompletionAsync(agentId, state.Loops, state.Cancellation.Token);

            await _agents.StopAgentAsync(agentId);

            if (completed)
                break;

            attempt++;
            currentGoal = $"{subgoal} (attempt {attempt})";
        }
    }

    private async Task<bool> WaitForCompletionAsync(string agentId, int loops, CancellationToken token)
    {
        var elapsed = 0;
        while (!token.IsCancellationRequested && elapsed < loops * 10000)
        {
            var logs = await _agents.GetAllMessagesAsync(agentId);
            if (logs.Any(l => l.Contains("LLM signaled DONE", StringComparison.OrdinalIgnoreCase) ||
                              l.Contains("Planner indicated completion", StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }

            if (logs.Any(l => l.Contains("Agent completed loops")))
                break;

            await Task.Delay(1000, token);
            elapsed += 1000;
        }

        return false;
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
