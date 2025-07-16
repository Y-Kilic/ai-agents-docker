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

            input = input.Trim();
            if ((input.StartsWith("\"") && input.EndsWith("\"")) || (input.StartsWith("'") && input.EndsWith("'")))
                input = input.Substring(1, input.Length - 2);

            // strip trailing comment if present
            var hashIndex = input.IndexOf('#');
            if (hashIndex >= 0)
                input = input.Substring(0, hashIndex).Trim();

            // automatically follow redirects for curl commands
            if (input.StartsWith("curl ", StringComparison.Ordinal) && !input.Contains(" -L"))
            {
                var withoutCurl = input.Substring(5);
                input = "curl -L " + withoutCurl;
            }

            var psi = new ProcessStartInfo
            {
                FileName = shell,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            if (OperatingSystem.IsWindows())
            {
                psi.ArgumentList.Add("/c");
                psi.ArgumentList.Add(input);
            }
            else
            {
                psi.ArgumentList.Add("-c");
                psi.ArgumentList.Add(input);
            }
            using var process = Process.Start(psi);
            if (process == null)
                return "{\"exit_code\":-1,\"stdout\":\"\",\"stderr\":\"Failed to start shell\"}";
            var output = await process.StandardOutput.ReadToEndAsync();
            var error = await process.StandardError.ReadToEndAsync();
            process.WaitForExit();

            const int limit = 200;
            var result = new
            {
                exit_code = process.ExitCode,
                stdout = output.Length > limit ? output[..limit] : output,
                stdout_trunc = output.Length > limit,
                stderr = error.Length > limit ? error[..limit] : error,
                stderr_trunc = error.Length > limit,
                side_effect = GetSideEffectSummary(input)
            };

            return System.Text.Json.JsonSerializer.Serialize(result);
        }
        catch (Exception ex)
        {
            var err = new { exit_code = -1, stdout = "", stderr = ex.Message };
            return System.Text.Json.JsonSerializer.Serialize(err);
        }
    }

    private static string? GetSideEffectSummary(string cmd)
    {
        try
        {
            // check simple patterns for file output
            string? path = null;
            if (cmd.Contains(" > "))
            {
                var parts = cmd.Split('>');
                path = parts[^1].Trim().Split(' ')[0];
            }
            else if (cmd.Contains(" -o "))
            {
                var parts = cmd.Split(" -o ");
                path = parts[^1].Trim().Split(' ')[0];
            }
            if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
            {
                var fi = new FileInfo(path);
                return $"wrote {fi.Length} bytes to {path}";
            }
        }
        catch { }
        return null;
    }
}
