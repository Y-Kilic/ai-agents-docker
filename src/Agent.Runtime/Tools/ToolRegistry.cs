using System.Collections.Concurrent;

namespace Agent.Runtime.Tools;

public static class ToolRegistry
{
    private static readonly ConcurrentDictionary<string, ITool> _tools = new();

    static ToolRegistry()
    {
        // Register built-in tools
        Register(new EchoTool());
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
}
