using Agent.Runtime.Tools;
using Shared.Models;
using Shared.LLM;
using System.Net.Http;
using System.Net.Http.Json;

var config = AgentProfiles.TryGetProfile(AgentType.Default, out var profile)
    ? profile
    : new AgentConfig("runtime", AgentType.Default);

var agentId = Environment.GetEnvironmentVariable("AGENT_ID");
var orchestratorUrl = Environment.GetEnvironmentVariable("ORCHESTRATOR_URL");
HttpClient? httpClient = null;
if (!string.IsNullOrWhiteSpace(agentId) && !string.IsNullOrWhiteSpace(orchestratorUrl))
{
    httpClient = new HttpClient { BaseAddress = new Uri(orchestratorUrl) };
}

await SendLogAsync($"Starting agent: {config.Name} ({config.Type})");

ILLMProvider llmProvider;
var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
if (string.IsNullOrWhiteSpace(apiKey))
{
    llmProvider = new MockOpenAIProvider();
    await SendLogAsync("Using MockOpenAIProvider");
}
else
{
    llmProvider = new OpenAIProvider(apiKey);
    await SendLogAsync("Using OpenAIProvider");
}

ToolRegistry.Initialize(llmProvider);

await RunAsync(args);

async Task SendLogAsync(string message)
{
    Console.WriteLine(message);
    if (httpClient != null && agentId != null)
    {
        try
        {
            await httpClient.PostAsJsonAsync($"api/message/{agentId}", message);
        }
        catch { }
    }
}

async Task SendMemoryAsync(string entry)
{
    if (httpClient != null && agentId != null)
    {
        try
        {
            await httpClient.PostAsJsonAsync($"api/memory/{agentId}", entry);
        }
        catch { }
    }
}

async Task RunAsync(string[] args)
{
    string goal = args.Length > 0
        ? string.Join(" ", args)
        : Environment.GetEnvironmentVariable("GOAL") ?? "echo hello";

    await SendLogAsync($"Goal received: {goal}");

    var memory = new List<string>();
    for (var i = 0; i < 3; i++)
    {
        var parts = goal.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
        {
            await SendLogAsync("No goal specified.");
            break;
        }

        var toolName = parts[0];
        var toolInput = parts.Length > 1 ? parts[1] : string.Empty;

        var tool = ToolRegistry.Get(toolName);
        if (tool is null)
        {
            await SendLogAsync($"Tool '{toolName}' not found.");
            break;
        }

        var result = await tool.ExecuteAsync(toolInput);
        memory.Add($"{toolName}:{toolInput} => {result}");
        await SendMemoryAsync($"{toolName}:{toolInput} => {result}");
        await SendLogAsync(result);

        goal = result;
    }

    await SendLogAsync("Memory:");
    foreach (var entry in memory)
        await SendLogAsync(entry);
}

