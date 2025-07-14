using Orchestrator.API.Data;
using Shared.Models;

namespace WorldSeed.Tests;

public class InMemoryUnitOfWorkTests
{
    [Fact]
    public async Task SaveChangesAsync_DoesNothing()
    {
        var uow = new InMemoryUnitOfWork();
        await uow.SaveChangesAsync();
        // Should not throw
    }

    [Fact]
    public void AgentsRepository_IsAccessible()
    {
        var uow = new InMemoryUnitOfWork();
        Assert.NotNull(uow.Agents);
    }
}
