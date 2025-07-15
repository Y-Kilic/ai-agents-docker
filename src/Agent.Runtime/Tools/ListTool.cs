using Shared.LLM;

namespace Agent.Runtime.Tools;

public class ListTool : ITool
{
    public string Name => "list";

    private readonly ILLMProvider _provider;

    public ListTool(ILLMProvider provider)
    {
        _provider = provider;
    }

    public async Task<string> ExecuteAsync(string input)
    {
        var prompt = $"List {input}. Provide short numbered items only.";
        return await _provider.CompleteAsync(prompt);
    }
}
