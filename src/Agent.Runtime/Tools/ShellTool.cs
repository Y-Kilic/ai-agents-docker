using System.Diagnostics;

namespace Agent.Runtime.Tools;

public class ShellTool : ITool
{
    public string Name => "shell";

    public async Task<string> ExecuteAsync(string input)
    {
        try
        {
            var psi = new ProcessStartInfo("/bin/bash", "-c \"" + input.Replace("\"", "\\\"") + "\"")
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
