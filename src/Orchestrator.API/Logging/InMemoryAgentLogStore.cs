using System.Collections.Concurrent;

namespace Orchestrator.API.Logging;

public class InMemoryAgentLogStore : IAgentLogStore
{
    private readonly ConcurrentDictionary<string, List<string>> _logs = new();

    public void Add(string agentId, string message)
    {
        var list = _logs.GetOrAdd(agentId, _ => new List<string>());
        lock (list)
        {
            list.Add(message);
        }
    }

    public IReadOnlyList<string> Get(string agentId)
    {
        return _logs.TryGetValue(agentId, out var list)
            ? list.AsReadOnly()
            : Array.Empty<string>();
    }
}
