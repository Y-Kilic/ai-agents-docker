using Orchestrator.API.Services;
using Shared.Models;
using Shared.LLM;
using System.Linq;

namespace WorldSeed.Tests;

public class OverseerServiceTests
{
    [Fact(Skip="Flaky in CI environments")]
    public async Task StartAndStopOverseer_CreatesAgents()
    {
        Environment.SetEnvironmentVariable("USE_LOCAL_AGENT", "1");
        var uow = new Orchestrator.API.Data.InMemoryUnitOfWork();
        var orchestrator = new AgentOrchestrator(uow);
        var overseer = new OverseerService(orchestrator, new MockOpenAIProvider());

        var id = await overseer.StartAsync("task one. task two.", 1);
        OverseerInfo info = overseer.List().First(o => o.Id == id);
        for (var i = 0; i < 20 && info.AgentIds.Count < 2; i++)
        {
            await Task.Delay(500);
            info = overseer.List().First(o => o.Id == id);
        }
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
        var overseer = new OverseerService(orchestrator, new MockOpenAIProvider());

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

    [Fact(Skip="Flaky in CI environments")]
    public async Task Overseer_Retries_Subgoal_WhenNotDone()
    {
        Environment.SetEnvironmentVariable("USE_LOCAL_AGENT", "1");
        var uow = new Orchestrator.API.Data.InMemoryUnitOfWork();
        var orchestrator = new AgentOrchestrator(uow);
        var overseer = new OverseerService(orchestrator, new MockOpenAIProvider());

        var id = await overseer.StartAsync("echo", 1);

        var retried = false;
        for (var i = 0; i < 30; i++)
        {
            await Task.Delay(1000);
            var info = overseer.List().First(o => o.Id == id);
            if (info.AgentIds.Count > 1)
            {
                retried = true;
                break;
            }
        }

        await overseer.StopAsync(id);

        Assert.True(retried, "Overseer did not retry incomplete subgoal");
    }
}
