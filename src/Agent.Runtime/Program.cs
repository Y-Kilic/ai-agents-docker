using Shared.Models;
using Shared.LLM;
using Agent.Runtime;

var config = AgentProfiles.TryGetProfile(AgentType.Default, out var profile)
    ? profile
    : new AgentConfig("runtime", AgentType.Default);

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

await RunAsync(args);

void SendLog(string message) => Console.WriteLine(message);

async Task RunAsync(string[] args)
{
    string goal = args.Length > 0
        ? string.Join(" ", args)
        : Environment.GetEnvironmentVariable("GOAL") ?? "echo hello";

    SendLog($"Goal received: {goal}");

    var loops = 5;
    if (int.TryParse(Environment.GetEnvironmentVariable("LOOP_COUNT"), out var parsed))
        loops = parsed; // 0 or negative = unlimited loops

    await AgentRunner.RunAsync(goal, llmProvider, loops, SendLog);
}
