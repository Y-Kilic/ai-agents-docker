using System.Diagnostics;
using System.IO;
using System.Text;

namespace Agent.Runtime.Tools;

public class TerminalTool : ITool
{
    public string Name => "terminal";

    /// <summary>
    /// The exit code from the last executed command.
    /// </summary>
    public int LastExitCode { get; private set; } = -1;

    private Process? _shell;
    private StreamWriter? _stdin;

    private void EnsureShell()
    {
        if (_shell != null && !_shell.HasExited)
            return;

        var psi = new ProcessStartInfo("bash")
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        psi.ArgumentList.Add("--noprofile");
        psi.ArgumentList.Add("--norc");
        psi.ArgumentList.Add("-s");

        _shell = Process.Start(psi);
        _stdin = _shell?.StandardInput;
    }

    public async Task<string> ExecuteAsync(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return "No command provided";

        input = Normalize(input);
        EnsureShell();
        if (_shell == null || _stdin == null)
            return "Failed to start shell";

        var sentinel = $"__EXIT_{Guid.NewGuid():N}";
        await _stdin.WriteLineAsync($"{{ {input}; }} 2>&1; echo {sentinel} $?");
        await _stdin.FlushAsync();

        var output = new StringBuilder();
        string? line;
        while ((line = await _shell.StandardOutput.ReadLineAsync()) != null)
        {
            if (line.StartsWith(sentinel))
            {
                var parts = line.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 2 && int.TryParse(parts[1], out var code))
                    LastExitCode = code;
                break;
            }
            output.AppendLine(line);
        }

        return output.ToString().TrimEnd();
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
