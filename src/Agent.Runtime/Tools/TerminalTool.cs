using System.Diagnostics;

namespace Agent.Runtime.Tools;

public class TerminalTool : ITool
{
    public string Name => "terminal";

    public async Task<string> ExecuteAsync(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return "No command provided";

        var psi = new ProcessStartInfo("bash", $"-c \"{input}\"")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };

        using var proc = Process.Start(psi);
        if (proc == null)
            return "Failed to start shell";
        string output = await proc.StandardOutput.ReadToEndAsync();
        string error = await proc.StandardError.ReadToEndAsync();
        await proc.WaitForExitAsync();
        return proc.ExitCode == 0 ? output : string.IsNullOrWhiteSpace(error) ? output : error;
    }
}
