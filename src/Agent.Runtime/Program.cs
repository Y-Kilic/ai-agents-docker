using Agent.Runtime.Tools;
using Shared.Models;
using Shared.LLM;
using System.Net.Http;
using System.Net.Http.Json;

var config = AgentProfiles.TryGetProfile(AgentType.Default, out var profile)
    ? profile
    : new AgentConfig("runtime", AgentType.Default);

// in the new pull model the runtime no longer posts data back to the host API
// so these variables are retained only for backwards compatibility
var agentId = Environment.GetEnvironmentVariable("AGENT_ID");
var orchestratorUrl = Environment.GetEnvironmentVariable("ORCHESTRATOR_URL");

SendLog($"Starting agent: {config.Name} ({config.Type})");

ILLMProvider llmProvider;
var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
if (string.IsNullOrWhiteSpace(apiKey))
{
    llmProvider = new MockOpenAIProvider();
    SendLog("Using MockOpenAIProvider");
}
else
{
    llmProvider = new OpenAIProvider(apiKey);
    SendLog("Using OpenAIProvider");
}

ToolRegistry.Initialize(llmProvider);

await RunAsync(args);

void SendLog(string message)
{
    Console.WriteLine(message);
}

async Task RunAsync(string[] args)
{
    string goal = args.Length > 0
        ? string.Join(" ", args)
        : Environment.GetEnvironmentVariable("GOAL") ?? "echo hello";

    SendLog($"Goal received: {goal}");

    var memory = new List<string>();
    var loops = 3;
    if (int.TryParse(Environment.GetEnvironmentVariable("LOOP_COUNT"), out var parsed))
        loops = parsed;

    for (var i = 0; i < loops; i++)
    {
        SendLog($"--- Loop {i + 1} of {loops} ---");

        var action = await PlanNextAction(goal, memory);
        SendLog($"Planner returned action: '{action}'");

        if (string.Equals(action, "done", StringComparison.OrdinalIgnoreCase))
        {
            SendLog("Planner indicated completion.");
            break;
        }

        var parts = action.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
        {
            SendLog("Planner returned no action.");
            break;
        }

        var toolName = parts[0];
        var toolInput = parts.Length > 1 ? parts[1] : string.Empty;

        var tool = ToolRegistry.Get(toolName);
        if (tool is null)
        {
            SendLog($"Tool '{toolName}' not found. Skipping this step.");
            memory.Add($"unknown:{toolName} -> no execution");
            continue;
        }

        var result = await tool.ExecuteAsync(toolInput);
        memory.Add($"{toolName}:{toolInput} => {result}");
        SendLog($"MEMORY: {toolName}:{toolInput} => {result}");
        SendLog(result);

        goal = result;
    }

    SendLog("--- Final Memory ---");
    foreach (var entry in memory)
        SendLog($"MEMORY: {entry}");
}

async Task<string> PlanNextAction(string currentGoal, List<string> memory)
{
    var tools = string.Join(", ", ToolRegistry.GetToolNames());
    var mem = memory.Count == 0 ? "none" : string.Join("; ", memory);
    var prompt = $"You are an autonomous agent. Current goal: '{currentGoal}'." +
        $" Past actions: {mem}. Available tools: {tools}." +
        " Choose the next tool and input in the format '<tool> <input>'." +
        " Reply with 'DONE' if the goal is complete.";
    SendLog($"PlanNextAction prompt: {prompt}");
    var result = await llmProvider.CompleteAsync(prompt);
    SendLog($"PlanNextAction result: {result}");
    return result.Trim();
}

