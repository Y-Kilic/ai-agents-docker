using Shared.LLM;

namespace WorldSeed.Tests;

public class LLMProviderTests
{
    [Fact]
    public async Task MockProvider_ReturnsMockResponse()
    {
        var provider = new MockOpenAIProvider("test");
        var result = await provider.CompleteAsync("prompt");
        Assert.Equal("test: prompt", result);
    }
}
