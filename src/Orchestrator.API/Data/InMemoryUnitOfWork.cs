namespace Orchestrator.API.Data;

public class InMemoryUnitOfWork : IUnitOfWork
{
    public IAgentRepository Agents { get; } = new InMemoryAgentRepository();

    public Task SaveChangesAsync() => Task.CompletedTask;
}
