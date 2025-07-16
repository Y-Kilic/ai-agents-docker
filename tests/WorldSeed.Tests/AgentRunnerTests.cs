using Agent.Runtime;
using Agent.Runtime.Tools;
using Shared.LLM;

namespace WorldSeed.Tests;

public class AgentRunnerTests
{
    [Fact]
    public async Task RunAsync_ExecutesShellTool()
    {
        var provider = new SequenceLLMProvider(new[] { "echo hi", "done" });
        var memory = await AgentRunner.RunAsync("test", provider, 2, _ => { });
        Assert.Contains(memory, m => m.Contains("\"stdout\":\"hi"));
        Assert.Contains(memory, m => m.Contains("\"stdout_trunc\":false"));
    }

    [Fact]
    public async Task RunAsync_ExecutesShellTool_WithQuotedInput()
    {
        var provider = new SequenceLLMProvider(new[] { "echo hi", "done" });
        var memory = await AgentRunner.RunAsync("test", provider, 2, _ => { });
        Assert.Contains(memory, m => m.Contains("\"stdout\":\"hi"));
        Assert.Contains(memory, m => m.Contains("\"stdout_trunc\":false"));
    }



    [Fact]
    public async Task RunAsync_LogsWhenDone()
    {
        var provider = new SequenceLLMProvider(new[] { "done" });
        var logs = new List<string>();
        await AgentRunner.RunAsync("test", provider, 0, logs.Add);
        Assert.Contains(logs, l => l.Contains("LLM signaled DONE"));
    }

    [Fact]
    public async Task RunAsync_HandlesRepeatedCommands()
    {
        var provider = new SequenceLLMProvider(new[] { "echo hi", "echo hi", "echo hi", "echo hi" });
        var logs = new List<string>();
        var memory = await AgentRunner.RunAsync("test", provider, 10, logs.Add);
        Assert.Contains(logs, l => l.Contains("Repeated command with no progress"));
        Assert.Contains(memory, m => m.StartsWith("repeat-detected"));
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
