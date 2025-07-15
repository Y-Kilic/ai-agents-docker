namespace Shared.Models;

public record OverseerStatus(OverseerInfo Info, Dictionary<string, List<string>> Logs);
