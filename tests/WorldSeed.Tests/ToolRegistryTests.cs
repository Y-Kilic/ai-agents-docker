using Agent.Runtime.Tools;
using Shared.LLM;

namespace WorldSeed.Tests;

public class ToolRegistryTests
{
    [Fact]
    public void Initialize_RegistersBuiltInTools()
    {
        ToolRegistry.Initialize(new MockOpenAIProvider());

        Assert.NotNull(ToolRegistry.Get("echo"));
        Assert.NotNull(ToolRegistry.Get("chat"));
        Assert.NotNull(ToolRegistry.Get("list"));

        // retrieval should be case-insensitive
        Assert.NotNull(ToolRegistry.Get("ECHO"));
        Assert.NotNull(ToolRegistry.Get("CHAT"));
        Assert.NotNull(ToolRegistry.Get("LIST"));
    }

    [Fact]
    public void Register_CustomTool_IsRetrievable()
    {
        ToolRegistry.Initialize(new MockOpenAIProvider());
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
