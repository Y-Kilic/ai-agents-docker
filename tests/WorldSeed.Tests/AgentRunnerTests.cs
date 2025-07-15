using Agent.Runtime;
using Shared.LLM;

namespace WorldSeed.Tests;

public class AgentRunnerTests
{
    [Fact]
    public async Task RunAsync_LoopsTwice_ReturnsCapitals()
    {
        var provider = new SequenceLLMProvider(new[]
        {
            "chat What is the capital of France?",
            "The capital of France is Paris.",
            "chat What is the capital of Belgium?",
            "The capital of Belgium is Brussels."
        });

        var memory = await AgentRunner.RunAsync(
            "What is the capital of france and belgium? reply in 2 loops.",
            provider,
            2,
            _ => { });

        Assert.Equal(2, memory.Count);
        Assert.Equal("chat What is the capital of France? => The capital of France is Paris.", memory[0]);
        Assert.Equal("chat What is the capital of Belgium? => The capital of Belgium is Brussels.", memory[1]);
    }

    [Fact]
    public async Task RunAsync_MissingTool_LogsAvailableTools()
    {
        var provider = new SequenceLLMProvider(new[]
        {
            "foo hello",
            "fallback"
        });

        var logs = new List<string>();
        await AgentRunner.RunAsync("test", provider, 1, logs.Add);

        Assert.Contains(logs, l => l.Contains("Unrecognized tool 'foo'"));
    }

    [Fact]
    public async Task RunAsync_ZeroLoops_RunsUntilDone()
    {
        var provider = new SequenceLLMProvider(new[]
        {
            "chat step one",
            "result one",
            "chat step two",
            "result two",
            "done"
        });

        var memory = await AgentRunner.RunAsync("test", provider, 0, _ => { });

        Assert.Equal(2, memory.Count);
        Assert.Equal("chat step one => result one", memory[0]);
        Assert.Equal("chat step two => result two", memory[1]);
    }

    [Fact]
    public async Task RunAsync_SendsHistoryToProviderEachLoop()
    {
        var provider = new RecordingLLMProvider(new[]
        {
            "chat hi",
            "pong",
            "done"
        });

        await AgentRunner.RunAsync("test", provider, 0, _ => { });

        Assert.Contains("History: none", provider.Prompts[1]);
        Assert.Contains("chat hi => pong", provider.Prompts[2]);
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

    private class RecordingLLMProvider : ILLMProvider
    {
        private readonly Queue<string> _responses;
        public List<string> Prompts { get; } = new();

        public RecordingLLMProvider(IEnumerable<string> responses)
        {
            _responses = new Queue<string>(responses);
        }

        public Task<string> CompleteAsync(string prompt, CancellationToken cancellationToken = default)
        {
            Prompts.Add(prompt);
            return Task.FromResult(_responses.Count > 0 ? _responses.Dequeue() : string.Empty);
        }
    }
}
