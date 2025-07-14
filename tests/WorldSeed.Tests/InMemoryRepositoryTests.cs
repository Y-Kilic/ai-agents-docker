using Orchestrator.API.Data;
using Shared.Models;

namespace WorldSeed.Tests;

public class InMemoryRepositoryTests
{
    [Fact]
    public void AddAndGetAgent_Works()
    {
        var repo = new InMemoryAgentRepository();
        var agent = new AgentInfo("id", AgentType.Default);
        repo.Add(agent);
        var retrieved = repo.Get("id");
        Assert.Equal(agent, retrieved);
        Assert.Single(repo.GetAll());
    }

    [Fact]
    public void RemoveAgent_Works()
    {
        var repo = new InMemoryAgentRepository();
        var agent = new AgentInfo("id", AgentType.Default);
        repo.Add(agent);
        repo.Remove("id");
        Assert.Empty(repo.GetAll());
    }
}
