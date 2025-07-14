using Shared.Models;

var config = AgentProfiles.TryGetProfile(AgentType.Default, out var profile)
    ? profile
    : new AgentConfig("runtime", AgentType.Default);

Console.WriteLine($"Starting agent: {config.Name} ({config.Type})");
