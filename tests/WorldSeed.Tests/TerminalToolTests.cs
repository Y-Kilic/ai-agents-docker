using Agent.Runtime.Tools;
using Xunit;

namespace WorldSeed.Tests;

public class TerminalToolTests
{
    [Fact]
    public async Task ExecuteAsync_RunsCommand()
    {
        var tool = new TerminalTool();
        var result = await tool.ExecuteAsync("echo hello");
        Assert.Contains("hello", result);
    }

    [Fact]
    public async Task ExecuteAsync_InvalidCommand_ReturnsError()
    {
        var tool = new TerminalTool();
        var result = await tool.ExecuteAsync("ls nonexistentfile");
        Assert.False(string.IsNullOrWhiteSpace(result));
    }
}
