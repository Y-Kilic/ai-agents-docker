using Agent.Runtime.Tools;
using Shared.Models;

var config = new AgentConfig("runtime");
Console.WriteLine($"Starting agent: {config.Name}");

await RunAsync(args);

static async Task RunAsync(string[] args)
{
    string goal = args.Length > 0
        ? string.Join(" ", args)
        : Environment.GetEnvironmentVariable("GOAL") ?? "echo hello";

    Console.WriteLine($"Goal received: {goal}");

    while (true)
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
        Console.WriteLine(result);

        // For this example we complete after one tool execution
        break;
    }
}
