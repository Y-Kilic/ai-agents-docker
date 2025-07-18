using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Shared.LLM;

namespace Agent.Runtime.Tools;

public static class ToolRegistry
{
    private static readonly ConcurrentDictionary<string, ITool> _tools = new(StringComparer.OrdinalIgnoreCase);
    private static Action<string> _log = Console.WriteLine;
    public static ILLMProvider? Provider { get; private set; }
    public static void Initialize(ILLMProvider llmProvider, List<string> memory, Action<string>? log = null)
    {
        _tools.Clear();
        _log = log ?? Console.WriteLine;
        Provider = llmProvider;
        // Register built-in tools
        Register(new EchoTool());
        Register(new ChatTool(llmProvider, memory));
        Register(new ListTool(llmProvider));
        Register(new CompareTool(llmProvider));
        Register(new WebTool());
        Register(new DotnetTool());
        // Load any external plugins from the plugins directory
        PluginLoader.LoadPlugins();
    }

    public static void Register(ITool tool)
    {
        _tools[tool.Name] = tool;
    }

    public static void Log(string message)
    {
        _log?.Invoke(message);
    }

    public static ITool? Get(string name)
    {
        _tools.TryGetValue(name, out var tool);
        return tool;
    }

    public static IEnumerable<string> GetToolNames() => _tools.Keys;
}
