namespace Shared.Models;

public record VmStatus(bool Enabled, bool BaseImagePresent, int RunningCount);
