using Shared.Models;
using Shared.LLM;
using System.Collections.Concurrent;
using System.Text.Json;

namespace Orchestrator.API.Services;

public class OverseerService
{
    private readonly AgentOrchestrator _agents;
    private readonly ILLMProvider _llm;

    private class OverseerState
    {
        public string Id { get; }
        public string Goal { get; }
        public int Loops { get; }
        public List<string> AgentIds { get; } = new();
        public List<string> Logs { get; } = new();
        public List<string> Results { get; } = new();
        public string? Result { get; set; }
        public CancellationTokenSource Cancellation { get; } = new();

        public OverseerState(string id, string goal, int loops)
        {
            Id = id;
            Goal = goal;
            Loops = loops;
        }
    }

    private readonly ConcurrentDictionary<string, OverseerState> _overseers = new();

    public OverseerService(AgentOrchestrator agents, ILLMProvider llmProvider)
    {
        _agents = agents;
        _llm = llmProvider;
    }

    public Task<string> StartAsync(string goal, int loops = 5)
    {
        var id = Guid.NewGuid().ToString("N");
        var state = new OverseerState(id, goal, loops);
        _overseers[id] = state;

        _ = Task.Run(() => RunAsync(state));

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

        return new OverseerStatus(info, logs, state.Logs.ToList(), state.Result);
    }

    public async Task StopAsync(string id)
    {
        if (!_overseers.TryRemove(id, out var state))
            return;

        state.Cancellation.Cancel();

        foreach (var agentId in state.AgentIds)
        {
            await _agents.StopAgentAsync(agentId);
            state.Logs.Add($"Stopped agent {agentId}");
        }
    }

    private async Task RunAsync(OverseerState state)
    {
        for (var i = 0; i < state.Loops && !state.Cancellation.IsCancellationRequested; i++)
        {
            state.Logs.Add($"Planning iteration {i + 1}");
            var plan = await PlanAsync(state);
            state.Logs.Add($"LLM: {plan}");

            if (plan.TrimStart().StartsWith("DONE", StringComparison.OrdinalIgnoreCase))
            {
                state.Result = plan.Trim();
                break;
            }

            await HandleSubgoalAsync(state, plan.Trim());
        }
    }

    private async Task HandleSubgoalAsync(OverseerState state, string subgoal)
    {
        var attempt = 1;
        var currentGoal = subgoal;
        while (!state.Cancellation.IsCancellationRequested)
        {
            var combinedGoal = $"Main goal: {state.Goal}\nSubgoal: {currentGoal}";
            var agentId = await _agents.StartAgentAsync(combinedGoal, AgentType.Default, state.Loops);
            state.AgentIds.Add(agentId);
            state.Logs.Add($"Started agent {agentId} for subgoal '{currentGoal}'");

            var completed = await WaitForCompletionAsync(agentId, state.Loops, state.Cancellation.Token);

            var logs = await _agents.GetAllMessagesAsync(agentId);
            if (logs.Count > 0)
                state.Results.Add(logs.Last());
            state.Logs.Add($"Agent {agentId} completed subgoal '{currentGoal}'");

            await _agents.StopAgentAsync(agentId);

            if (completed)
            {
                state.Logs.Add($"Subgoal '{subgoal}' completed");
                break;
            }

            attempt++;
            var lastLog = logs.LastOrDefault() ?? "none";
            var improvPrompt =
                $"We attempted the subgoal '{currentGoal}' but did not complete it. " +
                $"Last result: '{lastLog}'. " +
                "Suggest a new concise instruction that includes a measurable objective, " +
                "uses the \"load_trials\" helper from scripts/fetch_trials.py to obtain data, " +
                "and states a check that returns DONE when a 12-entry month dictionary is produced.";
            var newApproach = await _llm.CompleteAsync(improvPrompt);
            currentGoal = $"{newApproach.Trim()} (attempt {attempt})";
            state.Logs.Add($"Retrying subgoal '{subgoal}' as '{currentGoal}'");
        }
    }

    private async Task<string> PlanAsync(OverseerState state)
    {
        var context = state.Results.Count == 0 ? "none" : string.Join("; ", state.Results);
        var prompt = $"Goal: {state.Goal}. Previous results: {context}. " +
                     "Suggest the next subgoal in a short phrase that includes: " +
                     "a clear success metric, instructions to load data via the 'load_trials' helper in scripts/fetch_trials.py, " +
                     "and a check that returns DONE when a 12-entry month dictionary is produced. " +
                     "If the goal is accomplished respond with 'DONE: <result>'.";
        return await _llm.CompleteAsync(prompt);
    }

    private static bool LooksLikeMonthDict(string text)
    {
        try
        {
            using var doc = JsonDocument.Parse(text);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
                return false;
            return doc.RootElement.EnumerateObject().Count() == 12;
        }
        catch
        {
            return false;
        }
    }

    private async Task<bool> WaitForCompletionAsync(string agentId, int loops, CancellationToken token)
    {
        var elapsed = 0;
        while (!token.IsCancellationRequested && elapsed < loops * 5000)
        {
            var logs = await _agents.GetAllMessagesAsync(agentId);
            var last = logs.LastOrDefault();
            if (last != null && LooksLikeMonthDict(last))
                return true;
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

}
