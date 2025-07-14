using OpenAI_API;
using OpenAI_API.Chat;

namespace Shared.LLM;

public class OpenAIProvider : ILLMProvider
{
    private readonly OpenAIAPI _api;
    private readonly string _model;

    public OpenAIProvider(string apiKey, string model = "gpt-3.5-turbo")
    {
        _api = new OpenAIAPI(apiKey);
        _model = model;
    }

    public async Task<string> CompleteAsync(string prompt, CancellationToken cancellationToken = default)
    {
        var chat = _api.Chat.CreateConversation();
        chat.Model = _model;
        chat.AppendSystemMessage("You are a helpful assistant.");
        chat.AppendUserInput(prompt);
        var result = await chat.GetResponseFromChatbotAsync();
        return result;
    }
}
