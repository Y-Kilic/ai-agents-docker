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
    for (var i = 0; i < 3; i++)
    {
        var parts = goal.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
        {
            SendLog("No goal specified.");
            break;
        }

        var toolName = parts[0];
        var toolInput = parts.Length > 1 ? parts[1] : string.Empty;

        var tool = ToolRegistry.Get(toolName);
        if (tool is null)
        {
            SendLog($"Tool '{toolName}' not found.");
            break;
        }

        var result = await tool.ExecuteAsync(toolInput);
        memory.Add($"{toolName}:{toolInput} => {result}");
        SendLog($"MEMORY: {toolName}:{toolInput} => {result}");
        SendLog(result);

        goal = result;
    }

    SendLog("Memory:");
    foreach (var entry in memory)
        SendLog($"MEMORY: {entry}");
}

