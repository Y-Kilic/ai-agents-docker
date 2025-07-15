using Shared.LLM;

namespace Agent.Runtime.Tools;

public class CompareTool : ITool
{
    public string Name => "compare";

    private readonly ILLMProvider _provider;

    public CompareTool(ILLMProvider provider)
    {
        _provider = provider;
    }

    public async Task<string> ExecuteAsync(string input)
    {
        var prompt = $"Compare the following options and choose the best one with a short reason: {input}";
        return await _provider.CompleteAsync(prompt);
    }
}
