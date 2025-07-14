namespace Orchestrator.API.Data;

public interface IUnitOfWork
{
    IAgentRepository Agents { get; }
    Task SaveChangesAsync();
}
