namespace Shared.Models;

public record OverseerStatus(
    OverseerInfo Info,
    Dictionary<string, List<string>> Logs,
    List<string> OverseerLogs,
    string? Result);
