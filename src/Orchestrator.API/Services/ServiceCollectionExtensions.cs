using Microsoft.Extensions.DependencyInjection;

namespace Orchestrator.API.Services;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAgentOrchestrator(this IServiceCollection services)
    {
        services.AddSingleton<Data.IUnitOfWork, Data.InMemoryUnitOfWork>();
        services.AddSingleton<AgentOrchestrator>();

        var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        Shared.LLM.ILLMProvider llm = string.IsNullOrWhiteSpace(apiKey)
            ? new Shared.LLM.MockOpenAIProvider()
            : new Shared.LLM.OpenAIProvider(apiKey);
        services.AddSingleton(llm);

        services.AddSingleton<CodexService>();

        services.AddSingleton<OverseerService>();
        return services;
    }
}
