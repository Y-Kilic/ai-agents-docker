using Agent.Runtime.Tools;
using Shared.LLM;
using System;
using System.Linq;
using System.Collections.Generic;

namespace Agent.Runtime;

public static class AgentRunner
{
    private const int MaxMemoryChars = 8000;
    // Hard-fail if ANY of these are false.
    private const string Rubric = """
PASS when:
  • Program.cs is valid C# 12 targeting .NET 8
  • dotnet build -warnaserror succeeds with zero warnings
  • dotnet run -- "3+4*2" prints 11
  • dotnet run -- "(10/0)" prints Error (no exception)
Otherwise respond FAIL with a bullet list of problems.
""";

    private static async Task EnsureMemoryWithinLimit(List<string> memory, ILLMProvider llmProvider, Action<string> log)
    {
        var text = string.Join("; ", memory);
        while (text.Length > MaxMemoryChars * 2 && memory.Count > 1)
        {
            memory.RemoveAt(0);
            text = string.Join("; ", memory);
        }

        if (text.Length > MaxMemoryChars)
        {
            log("Memory too long, summarizing...");
            var summary = await llmProvider.CompleteAsync($"Summarize briefly: {text}");
            memory.Clear();
            memory.Add($"summary -> {summary}");
            log($"MEMORY: summary -> {summary}");
        }
    }
    public static async Task<List<string>> RunAsync(string goal, ILLMProvider llmProvider, int loops = 5, Action<string>? log = null)
    {
        log ??= Console.WriteLine;
        var memory = new List<string>();
        ToolRegistry.Initialize(llmProvider, memory, log);

        var i = 0;
        var unknownCount = 0;
        var critiqueFailures = 0;
        var nextTask = goal;
        var seenActions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
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
            bool duplicate = !seenActions.Add($"{toolName} {toolInput}");
            if (toolName.Equals("chat", StringComparison.OrdinalIgnoreCase) &&
                memory.LastOrDefault()?.StartsWith("chat", StringComparison.OrdinalIgnoreCase) == true)
                duplicate = true;   // consecutive chat ≈ no progress
            if (!madeProgress || duplicate)
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

            // Ask the LLM to explicitly state the intention before executing
            var planPrompt =
                $"In one short sentence, describe what you intend to accomplish by executing '{toolName} {toolInput}'.";
            var planNote = await llmProvider.CompleteAsync(planPrompt);
            memory.Add($"plan {toolName} {toolInput} -> {planNote}");
            log($"MEMORY: plan {toolName} {toolInput} -> {planNote}");

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
                    await EnsureMemoryWithinLimit(memory, llmProvider, log);
                    continue;
                }

                result = await chat.ExecuteAsync(action);
                memory.Add($"unknown {toolName} -> chat {action} => {result}");
                log($"MEMORY: unknown {toolName} -> chat {action} => {result}");
                await EnsureMemoryWithinLimit(memory, llmProvider, log);
            }
            else
            {
                result = await tool.ExecuteAsync(toolInput);
                if (toolName == "web")
                {
                    log("Summarizing website content...");
                    var summaryPrompt = $"Summarize the important information from this webpage for the goal '{goal}': {result}";
                    var summary = await llmProvider.CompleteAsync(summaryPrompt);
                    result = summary;
                }
                memory.Add($"{toolName} {toolInput} => {result}");
                log($"MEMORY: {toolName} {toolInput} => {result}");
                await EnsureMemoryWithinLimit(memory, llmProvider, log);

                string critique;
                if (toolName.Equals("dotnet", StringComparison.OrdinalIgnoreCase))
                {
                    // dotnet tool already returns PASS/FAIL header.
                    critique = result;
                }
                else
                {
                    // Ask the LLM to judge any other result against the rubric.
                    critique = await llmProvider.CompleteAsync($"Rubric:\n{Rubric}\nResult:\n{result}\nRespond PASS or FAIL with a short critique.");
                }
                memory.Add($"critique -> {critique}");
                log($"MEMORY: critique -> {critique}");
                if (critique.StartsWith("FAIL", StringComparison.OrdinalIgnoreCase))
                {
                    critiqueFailures++;
                    if (critiqueFailures >= 3)
                    {
                        log("Retry budget exhausted.");
                        break;
                    }
                }
                else
                {
                    critiqueFailures = 0;
                }

                executed = true;
            }

            log(result);
            if (toolName == "chat")
            {
                var canFinish = await llmProvider.CompleteAsync(
                    "Based on our conversation so far, can you state the single best option " +
                    "with a one-line rationale and then say DONE? Answer 'yes' or 'no'.");
                if (canFinish.TrimStart().StartsWith("y", StringComparison.OrdinalIgnoreCase))
                {
                    nextTask = "Answer the best option now and append DONE.";
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
        await EnsureMemoryWithinLimit(memory, llmProvider, log);

        var tools = string.Join(", ", ToolRegistry.GetToolNames());
        var mem = memory.Count == 0 ? "none" : string.Join("; ", memory);
        var prompt = $@"You are an autonomous agent working toward the goal: '{goal}'.
Loops remaining (including this one): {loopsLeft}.";
        if (loopsLeft != "unlimited")
            prompt += " You must complete the goal by the final loop.";
        prompt += $@"
Last result: '{context}'.
Past actions: {mem}.
Available tools: {tools}
All code MUST be valid C# 12 targeting .NET 8. Use only C# in your responses.
AFTER you output any code you MUST immediately call:
    dotnet
to build and run the tests; only after dotnet returns PASS may you declare DONE.
When calling the web tool, put the URL in quotes. Example: web ""https://example.com"".

**CRITICAL** – Finish in as few steps as possible.
        Respond ONLY with:
            <toolName> <input>
        or
            DONE";

        prompt += @"
You should answer DONE immediately when:
* You already possess enough information to recommend the single best option
  (write it with a one-line justification), OR
* The remaining unknowns would not change the top recommendation.
If a question still matters to rank items, ask it with 'chat'.";

        if (attempts > 0)
            prompt += " Your last response did not follow this format.";

        log($"PlanNextAction prompt: {prompt}");
        var result = await llmProvider.CompleteAsync(prompt);
        log($"PlanNextAction result: {result}");

        var line = result.Split('\n')[0].Trim().TrimEnd('.', '!');
        log($"PlanNextAction parsed line: {line}");

        var potentialToolName = line.Split(new[] { ' ', ':' }, 2, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        var potentialInput = line.Contains(' ') ? line.Substring(line.IndexOf(' ') + 1) : string.Empty;
        log($"Parsed toolName: '{potentialToolName}' input: '{potentialInput}'");

        if (potentialToolName == "chat" && potentialInput.Contains("```python"))
        {
            log("Planner tried to output Python; rejecting.");
            return await PlanNextAction(goal, context, loopsLeft, memory, llmProvider, log, attempts + 1);
        }

        if (result.Contains("DONE", StringComparison.OrdinalIgnoreCase))
        {
            log("LLM signaled DONE");
            return "done";
        }

        if (potentialToolName != null && !ToolRegistry.GetToolNames().Contains(potentialToolName, StringComparer.OrdinalIgnoreCase) && attempts < 2)
        {
            log($"Unrecognized tool '{potentialToolName}'. Retrying prompt.");
            return await PlanNextAction(goal, context, loopsLeft, memory, llmProvider, log, attempts + 1);
        }

        return line;
    }
}
