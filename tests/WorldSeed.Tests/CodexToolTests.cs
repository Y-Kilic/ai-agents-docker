using Codex.Plugin;
using Agent.Runtime.Tools;
using Shared.LLM;
using Xunit;
using System.Diagnostics;
using System.IO;

namespace WorldSeed.Tests;

public class CodexToolTests
{
    [Fact]
    public async Task Cat_ReturnsReadmeContent()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
        var readme = Path.Combine(root, "README.md");
        var tool = new CodexTool();
        var result = await tool.ExecuteAsync($"cat {readme}");
        Assert.Contains("Autonomous Agent Network Orchestrator", result);
    }

    [Fact]
    public async Task Generate_UsesProvider()
    {
        var provider = new MockOpenAIProvider("patch-text");
        ToolRegistry.Initialize(provider, new List<string>(), _ => { });
        var tool = new CodexTool();
        var result = await tool.ExecuteAsync("generate change something");
        Assert.Equal("patch-text", result);
    }

    [Fact]
    public async Task Annotate_ReturnsSummaryFromProvider()
    {
        var provider = new MockOpenAIProvider("summary");
        ToolRegistry.Initialize(provider, new List<string>(), _ => { });
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
        var readme = Path.Combine(root, "README.md");
        var tool = new CodexTool();
        var result = await tool.ExecuteAsync($"annotate {readme}");
        Assert.Equal("summary", result);
    }

    [Fact]
    public void Tools_ReturnsRegisteredNames()
    {
        ToolRegistry.Initialize(new MockOpenAIProvider(), new List<string>(), _ => { });
        PluginLoader.LoadPlugins(AppContext.BaseDirectory);
        var tool = new CodexTool();
        var result = tool.ExecuteAsync("tools").Result;
        Assert.Contains("codex", result);
        Assert.Contains("echo", result);
    }

    [Fact]
    public async Task Autopatch_AppliesPatch()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(tempDir);
        var originalDir = Directory.GetCurrentDirectory();
        Directory.SetCurrentDirectory(tempDir);

        Run("git init");
        await File.WriteAllTextAsync("test.txt", "old\n");
        Run("git add test.txt");
        Run("git commit -m init");

        var patch = "diff --git a/test.txt b/test.txt\nindex 3367afd..3e75765 100644\n--- a/test.txt\n+++ b/test.txt\n@@ -1 +1 @@\n-old\n+new\n";
        var provider = new MockOpenAIProvider(patch);
        ToolRegistry.Initialize(provider, new List<string>(), _ => { });
        var tool = new CodexTool();
        await tool.ExecuteAsync("autopatch change");

        var result = await File.ReadAllTextAsync("test.txt");
        Directory.SetCurrentDirectory(originalDir);
        Assert.Contains("new", result);
    }

    [Fact]
    public async Task Autopatch_WithFiles_UsesFileContext()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(tempDir);
        var originalDir = Directory.GetCurrentDirectory();
        Directory.SetCurrentDirectory(tempDir);

        Run("git init");
        await File.WriteAllTextAsync("test.txt", "old\n");
        Run("git add test.txt");
        Run("git commit -m init");

        var patch = "diff --git a/test.txt b/test.txt\nindex 3367afd..3e75765 100644\n--- a/test.txt\n+++ b/test.txt\n@@ -1 +1 @@\n-old\n+new\n";
        var provider = new RecordingProvider(patch);
        ToolRegistry.Initialize(provider, new List<string>(), _ => { });
        var tool = new CodexTool();
        await tool.ExecuteAsync("autopatch change --files test.txt");

        var result = await File.ReadAllTextAsync("test.txt");
        Directory.SetCurrentDirectory(originalDir);
        Assert.Contains("new", result);
        Assert.Contains("old", provider.LastPrompt);
    }

    [Fact(Skip="Commit operations unstable")]
    public async Task Autopatch_CommitsPatch()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(tempDir);
        var originalDir = Directory.GetCurrentDirectory();
        Directory.SetCurrentDirectory(tempDir);

        Run("git init");
        await File.WriteAllTextAsync("test.txt", "old\n");
        Run("git add test.txt");
        Run("git commit -m init");

        var patch = "diff --git a/test.txt b/test.txt\nindex 3367afd..3e75765 100644\n--- a/test.txt\n+++ b/test.txt\n@@ -1 +1 @@\n-old\n+new\n";
        var provider = new MockOpenAIProvider(patch);
        ToolRegistry.Initialize(provider, new List<string>(), _ => { });
        var tool = new CodexTool();
        await tool.ExecuteAsync("autopatch change --commit done");

        var log = RunCapture("git log --oneline -1");
        Directory.SetCurrentDirectory(originalDir);
        Assert.Contains("done", log);
    }

    [Fact]
    public async Task Run_BuildsAndExecutesProject()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(tempDir);
        var originalDir = Directory.GetCurrentDirectory();
        Directory.SetCurrentDirectory(tempDir);

        Run("dotnet new console -n HelloApp");
        var tool = new CodexTool();
        var result = await tool.ExecuteAsync($"run HelloApp/HelloApp.csproj");

        Directory.SetCurrentDirectory(originalDir);
        Assert.Contains("Hello, World", result);
    }

    private class RecordingProvider : ILLMProvider
    {
        private readonly string _resp;
        public string LastPrompt { get; private set; } = string.Empty;
        public RecordingProvider(string resp) => _resp = resp;
        public Task<string> CompleteAsync(string prompt, CancellationToken cancellationToken = default)
        {
            LastPrompt = prompt;
            return Task.FromResult(_resp);
        }
    }

    private static void Run(string cmd)
    {
        var parts = cmd.Split(' ', 2);
        var psi = new ProcessStartInfo(parts[0], parts.Length > 1 ? parts[1] : "")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        using var proc = Process.Start(psi);
        proc?.WaitForExit();
    }

    private static string RunCapture(string cmd)
    {
        var parts = cmd.Split(' ', 2);
        var psi = new ProcessStartInfo(parts[0], parts.Length > 1 ? parts[1] : "")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        using var proc = Process.Start(psi);
        var output = proc?.StandardOutput.ReadToEnd();
        proc?.WaitForExit();
        return output ?? string.Empty;
    }
}
