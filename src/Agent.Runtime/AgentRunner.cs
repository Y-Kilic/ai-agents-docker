using Agent.Runtime.Tools;
using Shared.LLM;
using System.Linq;
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
        ToolRegistry.Initialize(llmProvider, memory, log);

        var i = 0;
        var unknownCount = 0;
        var nextTask = goal;
        string? lastAction = null;
        string? lastResult = null;
        int repeatCount = 0;
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

            bool repeated = lastAction != null && lastAction.Equals($"{toolName} {toolInput}", StringComparison.OrdinalIgnoreCase);
            if (repeated)
            {
                repeatCount++;
                log("Same command repeated with no progress.");
                if (repeatCount >= 3)
                {
                    log("Stopping due to repeated command with no progress.");
                    break;
                }
            }
            else
            {
                repeatCount = 0;
            }

            log($"Looking up tool '{toolName}' among: {string.Join(", ", ToolRegistry.GetToolNames())}");

            // Ask the LLM to explicitly state the intention before executing
            var planPrompt =
                $"In one short sentence, describe what you intend to accomplish by executing '{toolName} {toolInput}'.";
            var planNote = await llmProvider.CompleteAsync(planPrompt);
            memory.Add($"plan {toolName} {toolInput} -> {planNote}");
            log($"MEMORY: plan {toolName} {toolInput} -> {planNote}");

            var tool = ToolRegistry.Get(toolName);
            string result = string.Empty;
            var executed = false;
            if (toolName.Equals("shell", StringComparison.OrdinalIgnoreCase) &&
                toolInput.Contains("install") && toolInput.Contains("curl"))
            {
                if (await CommandExistsAsync("curl"))
                {
                    result = "curl already installed";
                    memory.Add($"skip {toolInput} -> curl already installed");
                    log($"MEMORY: skip {toolInput} -> curl already installed");
                    executed = true;
                }
            }

            if (!executed)
            {
                if (tool is null)
                {
                    log($"Unknown tool response: '{action}'");
                    memory.Add($"unknown {toolName} -> no execution");
                    await EnsureMemoryWithinLimit(memory, llmProvider, log);
                }
                else
                {
                    result = await tool.ExecuteAsync(toolInput);
                    var shortResult = result.Length > 200 ? result.Substring(0, 200) + "..." : result;
                    memory.Add($"{toolName} {toolInput} => {shortResult}");
                    log($"MEMORY: {toolName} {toolInput} => {shortResult}");
                    await EnsureMemoryWithinLimit(memory, llmProvider, log);
                    executed = true;
                    log(shortResult);
                }
            }
            if (executed)
            {
                if (toolName.Equals("result", StringComparison.OrdinalIgnoreCase))
                {
                    log("Result tool executed. Stopping loop.");
                    break;
                }
                nextTask = $"Previous result: {result}. Determine the next step to achieve the goal.";
                i++;
                unknownCount = 0;
                lastAction = $"{toolName} {toolInput}";
                lastResult = result;
                if (result.Contains("DONE", StringComparison.OrdinalIgnoreCase))
                {
                    log("Result indicates DONE. Stopping loop.");
                    break;
                }
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
You can run any command in the container using the shell tool. Example: shell ""ls -la"".

**CRITICAL** - Finish in as few steps as possible.
        Respond ONLY with:
            <toolName> <input>
        or
            DONE";

        prompt += @"
You should answer DONE immediately when:
* You already possess enough information to recommend the single best option
  (write it with a one-line justification), OR
* The remaining unknowns would not change the top recommendation.
";

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

    private static bool _curlChecked;
    private static bool _curlAvailable;
    private static async Task<bool> CommandExistsAsync(string command)
    {
        if (!_curlChecked && command == "curl")
        {
            var psi = new ProcessStartInfo
            {
                FileName = "sh",
                ArgumentList = { "-c", $"command -v {command}" },
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            using var p = Process.Start(psi);
            if (p == null)
                return false;
            var output = await p.StandardOutput.ReadToEndAsync();
            p.WaitForExit();
            _curlAvailable = !string.IsNullOrWhiteSpace(output);
            _curlChecked = true;
        }

        if (command == "curl")
            return _curlAvailable;

        return false;
    }
}
