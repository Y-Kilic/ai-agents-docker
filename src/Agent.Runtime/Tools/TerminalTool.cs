using System.Diagnostics;

namespace Agent.Runtime.Tools;

public class TerminalTool : ITool
{
    public string Name => "terminal";

    /// <summary>
    /// The exit code from the last executed command.
    /// </summary>
    public int LastExitCode { get; private set; } = -1;

    public async Task<string> ExecuteAsync(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return "No command provided";

        input = Normalize(input);

        var psi = new ProcessStartInfo("bash")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        psi.ArgumentList.Add("-c");
        psi.ArgumentList.Add(input);

        using var proc = Process.Start(psi);
        if (proc == null)
            return "Failed to start shell";

        string output = await proc.StandardOutput.ReadToEndAsync();
        string error = await proc.StandardError.ReadToEndAsync();
        await proc.WaitForExitAsync();
        LastExitCode = proc.ExitCode;

        return proc.ExitCode == 0 ? output : string.IsNullOrWhiteSpace(error) ? output : error;
    }

    private static string Normalize(string cmd)
    {
        cmd = cmd.Trim();
        if ((cmd.StartsWith("\"") && cmd.EndsWith("\"")) ||
            (cmd.StartsWith("'") && cmd.EndsWith("'")))
        {
            cmd = cmd.Substring(1, cmd.Length - 2);
        }
        return cmd;
    }
}
