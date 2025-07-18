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
        // Returning the prompt verbatim caused tests relying on this
        // provider to detect keywords like "DONE" in the echoed text.
        // To avoid unintended behaviour we simply return the mock
        // response without appending the prompt.
        return Task.FromResult(_mockResponse);
    }
}
