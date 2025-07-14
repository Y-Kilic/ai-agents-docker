using Orchestrator.API.Services;
using Shared.Models;

namespace WorldSeed.Tests;

public class AgentOrchestratorTests
{
    [Fact]
    public async Task StartAndStopLocalAgent_Works()
    {
        Environment.SetEnvironmentVariable("USE_LOCAL_AGENT", "1");
        var uow = new Orchestrator.API.Data.InMemoryUnitOfWork();
        var orchestrator = new AgentOrchestrator(uow);

        var goalPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../mock-data/sample-goal.txt"));
        var goal = await File.ReadAllTextAsync(goalPath);
        var id = await orchestrator.StartAgentAsync(goal.Trim());

        Assert.Contains(uow.Agents.GetAll(), a => a.Id == id);

        await Task.Delay(1500);
        _ = await orchestrator.GetMessagesAsync(id);
        _ = await orchestrator.GetMemoryAsync(id);

        await orchestrator.StopAgentAsync(id);

        Assert.DoesNotContain(uow.Agents.GetAll(), a => a.Id == id);
    }
}
