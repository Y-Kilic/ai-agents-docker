namespace Orchestrator.API.Data;

using Shared.Models;

public interface IAgentRepository
{
    void Add(AgentInfo agent);
    void Remove(string id);
    IEnumerable<AgentInfo> GetAll();
    AgentInfo? Get(string id);
}
