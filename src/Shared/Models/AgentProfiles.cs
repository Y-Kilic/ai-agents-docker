using System.Collections.Generic;

namespace Shared.Models;

public static class AgentProfiles
{
    private static readonly Dictionary<AgentType, AgentConfig> _profiles = new()
    {
        { AgentType.Default, new AgentConfig("Default Agent", AgentType.Default) },
        { AgentType.Research, new AgentConfig("Research Agent", AgentType.Research) },
        { AgentType.Helper, new AgentConfig("Helper Agent", AgentType.Helper) }
    };

    public static bool TryGetProfile(AgentType type,
        [System.Diagnostics.CodeAnalysis.MaybeNullWhen(false)] out AgentConfig config)
        => _profiles.TryGetValue(type, out config);
}
