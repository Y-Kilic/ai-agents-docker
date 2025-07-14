namespace Shared.LLM;

public class MockOpenAIProvider : ILLMProvider
{
    private readonly string _mockResponse;

    public MockOpenAIProvider(string mockResponse = "Mock reply")
    {
        _mockResponse = mockResponse;
    }

    public Task<string> CompleteAsync(string prompt, CancellationToken cancellationToken = default)
    {
        return Task.FromResult($"{_mockResponse}: {prompt}");
    }
}
