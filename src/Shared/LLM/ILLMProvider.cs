namespace Shared.LLM;

public interface ILLMProvider
{
    Task<string> CompleteAsync(string prompt, CancellationToken cancellationToken = default);
}
