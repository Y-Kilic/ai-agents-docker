using Agent.Runtime.Tools;
using Shared.LLM;
using System.Linq;

namespace Agent.Runtime;

public static class AgentRunner
{
    public static async Task<List<string>> RunAsync(string goal, ILLMProvider llmProvider, int loops = 5, Action<string>? log = null)
    {
        log ??= Console.WriteLine;
        var memory = new List<string>();
        ToolRegistry.Initialize(llmProvider, memory);

        var i = 0;
        var unknownCount = 0;
        var nextTask = goal;
        while (loops <= 0 || i < loops)
        {
            var loopMessage = loops <= 0
                ? $"--- Starting loop {i + 1} ---"
                : $"--- Starting loop {i + 1} of {loops} ---";
            log(loopMessage);

            var action = await PlanNextAction(goal, nextTask, memory, llmProvider, log);
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
            var executed = false;
            if (tool is null)
            {
                log($"Unknown tool response: '{action}'");
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
                executed = true;
            }

            log(result);
            if (executed)
            {
                nextTask = $"Previous result: {result}. Determine the next step to achieve the goal.";
                i++;
                unknownCount = 0;
            }
            else
            {
                unknownCount++;
                log($"Unrecognized action count: {unknownCount}");
                if (unknownCount >= 3)
                {
                    log("Too many unrecognized actions. Stopping loop.");
                    break;
                }
            }
        }

        log("--- Final Memory ---");
        foreach (var entry in memory)
            log($"MEMORY: {entry}");

        return memory;
    }

    private static async Task<string> PlanNextAction(string goal, string context, List<string> memory, ILLMProvider llmProvider, Action<string> log, int attempts = 0)
    {
        var tools = string.Join(", ", ToolRegistry.GetToolNames());
        var mem = memory.Count == 0 ? "none" : string.Join("; ", memory);
        var prompt =
            $"You are an autonomous agent working toward the goal: '{goal}'." +
            $" The last result was: '{context}'." +
            $" Past actions: {mem}." +
            $" Available tools: {tools}." +
            " Decide on the next best tool and input to achieve the goal." +
            " Respond ONLY with '<tool> <input>' using one of the tool names above." +
            " If unsure which tool fits, use 'chat' with a helpful question." +
            " Reply with 'DONE' when the goal is complete.";

        if (attempts > 0)
            prompt += " Your last response did not follow this format.";

        log($"PlanNextAction prompt: {prompt}");
        var result = await llmProvider.CompleteAsync(prompt);
        log($"PlanNextAction result: {result}");

        var line = result.Split('\n')[0].Trim().Trim('"', '.', '!');
        log($"PlanNextAction parsed line: {line}");

        if (result.Contains("DONE", StringComparison.OrdinalIgnoreCase))
        {
            log("LLM signaled DONE");
            return "done";
        }

        var potentialToolName = line.Split(new[] { ' ', ':' }, 2, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        if (potentialToolName != null && !ToolRegistry.GetToolNames().Contains(potentialToolName, StringComparer.OrdinalIgnoreCase) && attempts < 2)
        {
            log($"Unrecognized tool '{potentialToolName}'. Retrying prompt.");
            return await PlanNextAction(goal, context, memory, llmProvider, log, attempts + 1);
        }

        return line;
    }
}
