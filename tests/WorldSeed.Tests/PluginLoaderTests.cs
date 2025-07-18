using Agent.Runtime.Tools;
using Codex.Plugin;
using Shared.LLM;

namespace WorldSeed.Tests;

public class PluginLoaderTests
{
    [Fact]
    public void LoadPlugins_RegistersCodexTool()
    {
        ToolRegistry.Initialize(new MockOpenAIProvider(), new List<string>(), _ => { });
        // Plugin DLLs are copied to the test output directory via project reference
        PluginLoader.LoadPlugins(AppContext.BaseDirectory);

        Assert.NotNull(ToolRegistry.Get("codex"));
    }
}
