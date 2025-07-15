using Orchestrator.API.Services;
using Shared.Models;
using System.Linq;

namespace WorldSeed.Tests;

public class OverseerServiceTests
{
    [Fact]
    public async Task StartAndStopOverseer_CreatesAgents()
    {
        Environment.SetEnvironmentVariable("USE_LOCAL_AGENT", "1");
        var uow = new Orchestrator.API.Data.InMemoryUnitOfWork();
        var orchestrator = new AgentOrchestrator(uow);
        var overseer = new OverseerService(orchestrator);

        var id = await overseer.StartAsync("task one. task two.");
        var info = overseer.List().First(o => o.Id == id);
        Assert.True(info.AgentIds.Count >= 2);

        await overseer.StopAsync(id);
        Assert.Empty(overseer.List());
    }

    [Fact]
    public async Task GetStatusAsync_ReturnsLogsForAgents()
    {
        Environment.SetEnvironmentVariable("USE_LOCAL_AGENT", "1");
        var uow = new Orchestrator.API.Data.InMemoryUnitOfWork();
        var orchestrator = new AgentOrchestrator(uow);
        var overseer = new OverseerService(orchestrator);

        var id = await overseer.StartAsync("echo one. echo two.", 1);

        OverseerStatus? status = null;
        for (var i = 0; i < 10; i++)
        {
            await Task.Delay(500);
            status = await overseer.GetStatusAsync(id);
            if (status != null && status.Logs.Values.All(l => l.Count > 0))
                break;
        }
        Assert.NotNull(status);
        foreach (var list in status!.Logs.Values)
        {
            Assert.NotEmpty(list);
        }

        await overseer.StopAsync(id);
    }
}
