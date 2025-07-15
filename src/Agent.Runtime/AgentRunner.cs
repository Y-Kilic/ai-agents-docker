using Agent.Runtime.Tools;
using Shared.LLM;

namespace Agent.Runtime;

public static class AgentRunner
{
    public static async Task<List<string>> RunAsync(string goal, ILLMProvider llmProvider, int loops = 3, Action<string>? log = null)
    {
        log ??= Console.WriteLine;
        var memory = new List<string>();
        ToolRegistry.Initialize(llmProvider, memory);

        var i = 0;
        while (loops <= 0 || i < loops)
        {
            var loopMessage = loops <= 0
                ? $"--- Starting loop {i + 1} ---"
                : $"--- Starting loop {i + 1} of {loops} ---";
            log(loopMessage);

            var action = await PlanNextAction(goal, memory, llmProvider, log);
            log($"Planner returned action: '{action}'");

            if (string.Equals(action, "done", StringComparison.OrdinalIgnoreCase))
            {
                log("Planner indicated completion.");
                break;
            }

            var parts = action.Split(new[] { ' ', ':' }, 2, StringSplitOptions.RemoveEmptyEntries);
            log($"Parsed parts: {string.Join(", ", parts)}");
            if (parts.Length == 0)
            {
                log("Planner returned no action.");
                break;
            }

            var toolName = parts[0];
            var toolInput = parts.Length > 1 ? parts[1] : string.Empty;
            log($"Parsed toolName: '{toolName}' input: '{toolInput}'");
            log($"Looking up tool '{toolName}' among: {string.Join(", ", ToolRegistry.GetToolNames())}");

            var tool = ToolRegistry.Get(toolName);
            string result;
            if (tool is null)
            {
                log($"Tool '{toolName}' not found. Falling back to chat.");
                var chat = ToolRegistry.Get("chat");
                if (chat is null)
                {
                    log("Chat tool is not registered. Skipping this step.");
                    memory.Add($"unknown {toolName} -> no execution");
                    continue;
                }

                result = await chat.ExecuteAsync(action);
                memory.Add($"unknown {toolName} -> chat {action} => {result}");
                log($"MEMORY: unknown {toolName} -> chat {action} => {result}");
            }
            else
            {
                result = await tool.ExecuteAsync(toolInput);
                memory.Add($"{toolName} {toolInput} => {result}");
                log($"MEMORY: {toolName} {toolInput} => {result}");
            }

            log(result);
            goal = result;
            i++;
        }

        log("--- Final Memory ---");
        foreach (var entry in memory)
            log($"MEMORY: {entry}");

        return memory;
    }

    private static async Task<string> PlanNextAction(string currentGoal, List<string> memory, ILLMProvider llmProvider, Action<string> log)
    {
        var tools = string.Join(", ", ToolRegistry.GetToolNames());
        var mem = memory.Count == 0 ? "none" : string.Join("; ", memory);
        var prompt =
            $"You are an autonomous agent. Current goal: '{currentGoal}'." +
            $" Past actions: {mem}." +
            $" Available tools: {tools}." +
            " Respond ONLY with '<tool> <input>' using one of the tool names above." +
            " If unsure which tool fits, use 'chat' with a helpful question." +
            " Reply with 'DONE' when the goal is complete.";
        log($"PlanNextAction prompt: {prompt}");
        var result = await llmProvider.CompleteAsync(prompt);
        log($"PlanNextAction result: {result}");

        var line = result.Split('\n')[0].Trim().Trim('"', '.', '!');
        log($"PlanNextAction parsed line: {line}");
        return line;
    }
}
