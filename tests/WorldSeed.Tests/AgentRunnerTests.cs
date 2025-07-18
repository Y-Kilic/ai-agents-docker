using Agent.Runtime;
using Agent.Runtime.Tools;
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
            "plan1",
            "The capital of France is Paris.",
            "PASS",
            "no",
            "chat What is the capital of Belgium?",
            "plan2",
            "The capital of Belgium is Brussels.",
            "PASS",
            "no"
        });

        var memory = await AgentRunner.RunAsync(
            "What is the capital of france and belgium? reply in 2 loops.",
            provider,
            2,
            _ => { });

        Assert.Contains("chat What is the capital of France? => The capital of France is Paris.", memory);
        Assert.Contains("chat What is the capital of Belgium? => The capital of Belgium is Brussels.", memory);
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
    public async Task RunAsync_UnrecognizedTool_DoesNotCountLoop()
    {
        var provider = new SequenceLLMProvider(new[]
        {
            "foo greet",
            "plan1",
            "ignored",
            "chat hi",
            "plan2",
            "pong",
            "no"
        });

        var memory = await AgentRunner.RunAsync("test", provider, 1, _ => { });

        Assert.True(memory.Count >= 1);
    }

    [Fact]
    public async Task RunAsync_ZeroLoops_RunsUntilDone()
    {
        var provider = new SequenceLLMProvider(new[]
        {
            "chat step one",
            "plan1",
            "result one",
            "PASS",
            "no",
            "chat step two",
            "plan2",
            "result two",
            "PASS",
            "no",
            "done"
        });

        var memory = await AgentRunner.RunAsync("test", provider, 0, _ => { });

        Assert.Contains("chat step one => result one", memory);
        Assert.Contains("chat step two => result two", memory);
    }

    [Fact]
    public async Task RunAsync_SendsHistoryToProviderEachLoop()
    {
        var provider = new RecordingLLMProvider(new[]
        {
            "chat hi",
            "plan",
            "pong",
            "no",
            "done"
        });

        await AgentRunner.RunAsync("test", provider, 0, _ => { });

        Assert.Contains(provider.Prompts, p => p.Contains("History:"));
        Assert.Contains(provider.Prompts, p => p.Contains("chat hi => pong"));
    }

    [Fact]
    public async Task RunAsync_SummarizesLongMemory()
    {
        var longText = new string('a', 9001);
        var provider = new SequenceLLMProvider(new[]
        {
            "chat hi",
            "plan1",
            longText,
            "no",
            "done"
        });

        var memory = await AgentRunner.RunAsync("test", provider, 0, _ => { });

        Assert.Contains("summary ->", memory[0]);
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
    public async Task RunAsync_WebCommandWithoutQuotes_ParsesCorrectly()
    {
        var provider = new RegisteringProvider("web https://example.com");
        await AgentRunner.RunAsync("test", provider, 1, _ => { });

        Assert.Equal("https://example.com", RegisteringProvider.CapturedInput);
    }

    [Fact]
    public async Task RunAsync_WebResult_IsSummarized()
    {
        var provider = new SequenceRegisteringProvider(new[]
        {
            "web https://example.com",
            "plan",
            "summary"
        });

        var memory = await AgentRunner.RunAsync("test", provider, 1, _ => { });

        Assert.Equal("web https://example.com => summary", memory[1]);
    }

    [Fact]
    public async Task RunAsync_AddsCritiqueToMemory()
    {
        var provider = new SequenceLLMProvider(new[]
        {
            "echo hi",
            "note",
            "PASS good"
        });

        var memory = await AgentRunner.RunAsync("test", provider, 1, _ => { });

        Assert.Contains(memory, m => m.StartsWith("critique ->"));
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

    private class SequenceRegisteringProvider : ILLMProvider
    {
        private readonly Queue<string> _responses;
        private bool _firstCall = true;

        public SequenceRegisteringProvider(IEnumerable<string> responses)
        {
            _responses = new Queue<string>(responses);
        }

        public Task<string> CompleteAsync(string prompt, CancellationToken cancellationToken = default)
        {
            if (_firstCall)
            {
                _firstCall = false;
                ToolRegistry.Register(new FakeWebTool());
            }
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

    private class RegisteringProvider : ILLMProvider
    {
        private readonly string _response;
        private bool _firstCall = true;
        public static string? CapturedInput { get; set; }

        public RegisteringProvider(string response)
        {
            _response = response;
        }

        public Task<string> CompleteAsync(string prompt, CancellationToken cancellationToken = default)
        {
            if (_firstCall)
            {
                _firstCall = false;
                ToolRegistry.Register(new FakeWebTool());
                return Task.FromResult(_response);
            }

            return Task.FromResult(string.Empty);
        }
    }

    private class FakeWebTool : ITool
    {
        public string Name => "web";
        public static string? LastInput { get; set; }

        public Task<string> ExecuteAsync(string input)
        {
            RegisteringProvider.CapturedInput = input;
            LastInput = input;
            return Task.FromResult("ok");
        }
    }
}
