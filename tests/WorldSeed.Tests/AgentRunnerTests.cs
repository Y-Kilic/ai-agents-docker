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
