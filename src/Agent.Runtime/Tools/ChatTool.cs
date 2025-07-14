using Shared.LLM;

namespace Agent.Runtime.Tools;

public class ChatTool : ITool
{
    public string Name => "chat";

    private readonly ILLMProvider _provider;

    public ChatTool(ILLMProvider provider)
    {
        _provider = provider;
    }

    public async Task<string> ExecuteAsync(string input)
    {
        return await _provider.CompleteAsync(input);
    }
}
