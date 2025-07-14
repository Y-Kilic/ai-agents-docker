using Shared.Models;

namespace WorldSeed.Tests;

public class AgentProfilesTests
{
    [Theory]
    [InlineData(AgentType.Default)]
    [InlineData(AgentType.Research)]
    [InlineData(AgentType.Helper)]
    public void TryGetProfile_ReturnsProfile(AgentType type)
    {
        var result = AgentProfiles.TryGetProfile(type, out var config);
        Assert.True(result);
        Assert.NotNull(config);
        Assert.Equal(type, config!.Type);
    }

    [Fact]
    public void TryGetProfile_Unknown_ReturnsFalse()
    {
        var result = AgentProfiles.TryGetProfile((AgentType)999, out var config);
        Assert.False(result);
        Assert.Null(config);
    }
}
