namespace Orchestrator.API.Logging;

public interface IAgentLogStore
{
    void Add(string agentId, string message);
    IReadOnlyList<string> Get(string agentId);
}
