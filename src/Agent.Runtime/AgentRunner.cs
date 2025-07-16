using Agent.Runtime.Tools;
using Shared.LLM;
using System.Diagnostics;

namespace Agent.Runtime;

public static class AgentRunner
{
    private const int MaxMemoryChars = 8000;

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
        var shell = new ShellTool();

        var i = 0;
        var nextTask = goal;
        string? lastCommand = null;
        string? lastResult = null;
        int repeatCount = 0;
        while (loops <= 0 || i < loops)
        {
            var loopMessage = loops <= 0
                ? $"--- Starting loop {i + 1} ---"
                : $"--- Starting loop {i + 1} of {loops} ---";
            log(loopMessage);

            var loopsLeft = loops <= 0 ? "unlimited" : (loops - i).ToString();
            var command = await PlanNextAction(goal, nextTask, loopsLeft, memory, llmProvider, log);
            log($"Planner returned command: '{command}'");

            if (string.Equals(command, "done", StringComparison.OrdinalIgnoreCase))
            {
                log("LLM signaled DONE");
                break;
            }

            var result = await shell.ExecuteAsync(command);
            if (command == lastCommand && result == lastResult)
            {
                repeatCount++;
                if (repeatCount >= 3)
                {
                    log("Repeated command with no progress. Asking for new approach.");
                    memory.Add($"repeat-detected -> {command}");
                    nextTask = $"The command '{command}' was repeated {repeatCount} times with no progress. Try a different approach to achieve the goal.";
                    repeatCount = 0;
                    lastCommand = null;
                    lastResult = null;
                    continue;
                }
            }
            else
            {
                repeatCount = 0;
                lastCommand = command;
                lastResult = result;
            }
            var shortResult = result.Length > 200 ? result.Substring(0, 200) + "..." : result;
            memory.Add($"{command} => {shortResult}");
            log($"MEMORY: {command} => {shortResult}");
            await EnsureMemoryWithinLimit(memory, llmProvider, log);
            log(shortResult);

            nextTask = $"Previous result: {result}. Determine the next step to achieve the goal.";
            i++;
        }

        log("--- Final Memory ---");
        foreach (var entry in memory)
            log($"MEMORY: {entry}");

        return memory;
    }

    private static async Task<string> PlanNextAction(string goal, string context, string loopsLeft, List<string> memory, ILLMProvider llmProvider, Action<string> log, int attempts = 0)
    {
        await EnsureMemoryWithinLimit(memory, llmProvider, log);

        var mem = memory.Count == 0 ? "none" : string.Join("; ", memory);
        var prompt = $@"You are an autonomous agent working toward the goal: '{goal}'.
Loops remaining (including this one): {loopsLeft}.";
        if (loopsLeft != "unlimited")
            prompt += " You must complete the goal by the final loop.";
        prompt += $@"
Last result: '{context}'.
Past actions: {mem}.
You have direct terminal access. Respond with the next command to run or DONE.
**CRITICAL** - Finish in as few steps as possible.";

        if (attempts > 0)
            prompt += " Your last response did not follow this format.";

        log($"PlanNextAction prompt: {prompt}");
        var result = await llmProvider.CompleteAsync(prompt);
        log($"PlanNextAction result: {result}");

        var line = result.Split('\n')[0].Trim().TrimEnd('.', '!');
        log($"PlanNextAction parsed line: {line}");

        if (line.Equals("DONE", StringComparison.OrdinalIgnoreCase))
        {
            return "done";
        }

        return line;
    }
}
