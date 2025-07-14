using Microsoft.Extensions.DependencyInjection;

namespace Orchestrator.API.Services;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAgentOrchestrator(this IServiceCollection services)
    {
        services.AddSingleton<AgentOrchestrator>();
        return services;
    }
}
