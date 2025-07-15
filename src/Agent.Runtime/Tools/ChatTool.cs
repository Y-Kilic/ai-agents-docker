using Shared.LLM;

namespace Agent.Runtime.Tools;

public class ChatTool : ITool
{
    public string Name => "chat";

    private readonly ILLMProvider _provider;
    private readonly List<string> _memory;

    public ChatTool(ILLMProvider provider, List<string> memory)
    {
        _provider = provider;
        _memory = memory;
    }

    public async Task<string> ExecuteAsync(string input)
    {
        var history = _memory.Count == 0 ? "none" : string.Join("; ", _memory);
        var prompt = $"History: {history}. User: {input}";
        return await _provider.CompleteAsync(prompt);
    }
}
