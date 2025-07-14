using Shared.Messaging;

namespace WorldSeed.Tests;

public class MemoryHubTests
{
    [Fact]
    public void SendAndReceive_ShouldDeliverEntries()
    {
        var id = Guid.NewGuid().ToString();
        MemoryHub.Send(id, "first");
        var entries = MemoryHub.Receive(id);
        Assert.Single(entries);
        Assert.Equal("first", entries[0]);
    }
}
