using System;
using System.Collections.Concurrent;
using Shared.LLM;

namespace Agent.Runtime.Tools;

public static class ToolRegistry
{
    private static readonly ConcurrentDictionary<string, ITool> _tools = new(StringComparer.OrdinalIgnoreCase);

    public static void Initialize(ILLMProvider llmProvider)
    {
        _tools.Clear();
        // Register built-in tools
        Register(new EchoTool());
        Register(new ChatTool(llmProvider));
        Register(new ListTool(llmProvider));
    }

    public static void Register(ITool tool)
    {
        _tools[tool.Name] = tool;
    }

    public static ITool? Get(string name)
    {
        _tools.TryGetValue(name, out var tool);
        return tool;
    }

    public static IEnumerable<string> GetToolNames() => _tools.Keys;
}
