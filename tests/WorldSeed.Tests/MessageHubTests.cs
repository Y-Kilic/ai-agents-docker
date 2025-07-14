using Shared.Messaging;

namespace WorldSeed.Tests;

public class MessageHubTests
{
    [Fact]
    public void SendAndReceive_ShouldDeliverMessages()
    {
        var id = Guid.NewGuid().ToString();
        MessageHub.Send(id, "hello");
        var msgs = MessageHub.Receive(id);
        Assert.Single(msgs);
        Assert.Equal("hello", msgs[0]);
    }
}
