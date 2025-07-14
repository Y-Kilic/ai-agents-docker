using Agent.Runtime.Tools;
using Shared.Models;
using Shared.LLM;

var config = AgentProfiles.TryGetProfile(AgentType.Default, out var profile)
    ? profile
    : new AgentConfig("runtime", AgentType.Default);

Console.WriteLine($"Starting agent: {config.Name} ({config.Type})");

ILLMProvider llmProvider;
var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
if (string.IsNullOrWhiteSpace(apiKey))
{
    llmProvider = new MockOpenAIProvider();
    Console.WriteLine("Using MockOpenAIProvider");
}
else
{
    llmProvider = new OpenAIProvider(apiKey);
    Console.WriteLine("Using OpenAIProvider");
}

ToolRegistry.Initialize(llmProvider);

await RunAsync(args);

static async Task RunAsync(string[] args)
{
    string goal = args.Length > 0
        ? string.Join(" ", args)
        : Environment.GetEnvironmentVariable("GOAL") ?? "echo hello";

    Console.WriteLine($"Goal received: {goal}");

    var memory = new List<string>();
    for (var i = 0; i < 3; i++)
    {
        var parts = goal.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
        {
            Console.WriteLine("No goal specified.");
            break;
        }

        var toolName = parts[0];
        var toolInput = parts.Length > 1 ? parts[1] : string.Empty;

        var tool = ToolRegistry.Get(toolName);
        if (tool is null)
        {
            Console.WriteLine($"Tool '{toolName}' not found.");
            break;
        }

        var result = await tool.ExecuteAsync(toolInput);
        memory.Add($"{toolName}:{toolInput} => {result}");
        Console.WriteLine(result);

        goal = result;
    }

    Console.WriteLine("Memory:");
    foreach (var entry in memory)
        Console.WriteLine(entry);
}

