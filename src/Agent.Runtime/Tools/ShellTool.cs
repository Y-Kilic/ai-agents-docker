using System.Diagnostics;

namespace Agent.Runtime.Tools;

public class ShellTool : ITool
{
    public string Name => "shell";

    public async Task<string> ExecuteAsync(string input)
    {
        try
        {
            var shell = Environment.GetEnvironmentVariable("SHELL");
            if (string.IsNullOrWhiteSpace(shell))
            {
                if (OperatingSystem.IsWindows())
                    shell = "cmd.exe";
                else if (File.Exists("/bin/bash"))
                    shell = "/bin/bash";
                else
                    shell = "/bin/sh";
            }

            var args = OperatingSystem.IsWindows()
                ? $"/c {input}"
                : $"-c \"{input.Replace("\"", "\\\"")}\"";

            var psi = new ProcessStartInfo(shell, args)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            using var process = Process.Start(psi);
            if (process == null)
                return "Failed to start shell";
            var output = await process.StandardOutput.ReadToEndAsync();
            var error = await process.StandardError.ReadToEndAsync();
            process.WaitForExit();
            var result = (output + error).Trim();
            return string.IsNullOrWhiteSpace(result) ? "(no output)" : result;
        }
        catch (Exception ex)
        {
            return $"Shell error: {ex.Message}";
        }
    }
}
