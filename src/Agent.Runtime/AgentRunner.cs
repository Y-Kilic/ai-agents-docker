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

            var loopsLeft = loops <= 0 ? "unlimited" : (loops - i).ToString();
            var action = await PlanNextAction(goal, nextTask, loopsLeft, memory, llmProvider, log);
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

            bool madeProgress = !memory.LastOrDefault()?.StartsWith($"{toolName} {toolInput}", StringComparison.OrdinalIgnoreCase) ?? true;
            if (!madeProgress)
            {
                toolName = "chat";
                toolInput = $"We just repeated the same command and made no progress. Summarise what we know and decide the next DISTINCT step toward '{goal}'.";
            }

            var lowValue =
                toolName.Equals("list", StringComparison.OrdinalIgnoreCase) && memory.Any(m => m.StartsWith("list ")) ||
                toolName.Equals("echo", StringComparison.OrdinalIgnoreCase) && !memory.Any(m => m.StartsWith("chat "));

            if (lowValue)
            {
                log($"'{toolName}' now would yield no new insight. Switching to chat.");
                toolName = "chat";
                toolInput = $"Using '{toolName}' here is redundant. Propose a DIFFERENT step or say DONE if we can finish.";
            }

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
            if (toolName == "chat")
            {
                var canFinish = await llmProvider.CompleteAsync(
                    "Based on our conversation so far, can you state the single best supplement " +
                    "with a one-line rationale and then say DONE? Answer 'yes' or 'no'.");
                if (canFinish.TrimStart().StartsWith("y", StringComparison.OrdinalIgnoreCase))
                {
                    nextTask = "Answer the best supplement now and append DONE.";
                }
            }
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

    private static async Task<string> PlanNextAction(string goal, string context, string loopsLeft, List<string> memory, ILLMProvider llmProvider, Action<string> log, int attempts = 0)
    {
        var tools = string.Join(", ", ToolRegistry.GetToolNames());
        var mem = memory.Count == 0 ? "none" : string.Join("; ", memory);
        var prompt = $@"You are an autonomous agent working toward the goal: '{goal}'.
Loops remaining (including this one): {loopsLeft}.
Last result: '{context}'.
Past actions: {mem}.
Available tools: {tools}

**CRITICAL** – Finish in as few steps as possible.
Respond ONLY with:
    <toolName> <input>
or
    DONE";

        prompt += @"
You should answer DONE immediately when:
* You already possess enough information to recommend the single best supplement
  (write it with a one-line justification), OR
* The remaining unknowns would not change the top recommendation.
If a question still matters to rank items, ask it with 'chat'.";

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
            return await PlanNextAction(goal, context, loopsLeft, memory, llmProvider, log, attempts + 1);
        }

        return line;
    }
}
