using Agent.Runtime.Tools;
using Shared.LLM;

namespace WorldSeed.Tests;

public class ToolRegistryTests
{
    [Fact]
    public void Initialize_RegistersBuiltInTools()
    {
        ToolRegistry.Initialize(new MockOpenAIProvider(), new List<string>(), _ => { });

        Assert.NotNull(ToolRegistry.Get("echo"));
        Assert.NotNull(ToolRegistry.Get("chat"));
        Assert.NotNull(ToolRegistry.Get("list"));
        Assert.NotNull(ToolRegistry.Get("compare"));
        Assert.NotNull(ToolRegistry.Get("web"));
        Assert.NotNull(ToolRegistry.Get("terminal"));

        // retrieval should be case-insensitive
        Assert.NotNull(ToolRegistry.Get("ECHO"));
        Assert.NotNull(ToolRegistry.Get("CHAT"));
        Assert.NotNull(ToolRegistry.Get("LIST"));
        Assert.NotNull(ToolRegistry.Get("COMPARE"));
        Assert.NotNull(ToolRegistry.Get("WEB"));
        Assert.NotNull(ToolRegistry.Get("TERMINAL"));
    }

    [Fact]
    public void Register_CustomTool_IsRetrievable()
    {
        ToolRegistry.Initialize(new MockOpenAIProvider(), new List<string>(), _ => { });
        var custom = new CustomTool();
        ToolRegistry.Register(custom);

        var retrieved = ToolRegistry.Get("custom");
        Assert.Equal(custom, retrieved);
    }

    private class CustomTool : ITool
    {
        public string Name => "custom";
        public Task<string> ExecuteAsync(string input) => Task.FromResult(input);
    }
}
