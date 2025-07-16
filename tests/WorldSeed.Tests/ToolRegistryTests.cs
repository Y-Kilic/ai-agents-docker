using Agent.Runtime.Tools;
using Shared.LLM;

namespace WorldSeed.Tests;

public class ToolRegistryTests
{
    [Fact]
    public void Initialize_RegistersBuiltInTools()
    {
        ToolRegistry.Initialize(new MockOpenAIProvider(), new List<string>(), _ => { });

        Assert.NotNull(ToolRegistry.Get("shell"));

        // retrieval should be case-insensitive
        Assert.NotNull(ToolRegistry.Get("SHELL"));
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
