using Agent.Runtime;
using Agent.Runtime.Tools;
using Shared.LLM;

namespace WorldSeed.Tests;

public class AgentRunnerTests
{
    [Fact]
    public async Task RunAsync_ExecutesShellTool()
    {
        var provider = new SequenceLLMProvider(new[] { "shell echo hi", "done" });
        var memory = await AgentRunner.RunAsync("test", provider, 2, _ => { });
        Assert.Contains("shell echo hi => hi", memory);
    }

    [Fact]
    public async Task RunAsync_ExecutesShellTool_WithQuotedInput()
    {
        var provider = new SequenceLLMProvider(new[] { "shell \"echo hi\"", "done" });
        var memory = await AgentRunner.RunAsync("test", provider, 2, _ => { });
        Assert.Contains("shell \"echo hi\" => hi", memory);
    }

    [Fact]
    public async Task RunAsync_MissingTool_LogsAvailableTools()
    {
        var provider = new SequenceLLMProvider(new[] { "foo bar", "done" });
        var logs = new List<string>();
        await AgentRunner.RunAsync("test", provider, 1, logs.Add);
        Assert.Contains(logs, l => l.Contains("Unrecognized tool"));
    }

    [Fact]
    public async Task RunAsync_LogsWhenDone()
    {
        var provider = new SequenceLLMProvider(new[] { "done" });
        var logs = new List<string>();
        await AgentRunner.RunAsync("test", provider, 0, logs.Add);
        Assert.Contains(logs, l => l.Contains("LLM signaled DONE"));
    }

    private class SequenceLLMProvider : ILLMProvider
    {
        private readonly Queue<string> _responses;
        public SequenceLLMProvider(IEnumerable<string> responses)
        {
            _responses = new Queue<string>(responses);
        }
        public Task<string> CompleteAsync(string prompt, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_responses.Count > 0 ? _responses.Dequeue() : string.Empty);
        }
    }
}
