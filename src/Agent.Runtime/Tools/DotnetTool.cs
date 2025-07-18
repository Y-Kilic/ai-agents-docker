using System.Diagnostics;
using System.Threading.Tasks;

namespace Agent.Runtime.Tools;

public sealed class DotnetTool : ITool
{
    public string Name => "dotnet";

    public async Task<string> ExecuteAsync(string _)
    {
        var psi = new ProcessStartInfo("bash", "-c \"dotnet build -warnaserror && dotnet run -- '3+4*2' && dotnet run -- '(10/0)'\"")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        using var proc = Process.Start(psi);
        if (proc == null)
            return "FAIL\nCould not start dotnet";
        string output = await proc.StandardOutput.ReadToEndAsync();
        string error = await proc.StandardError.ReadToEndAsync();
        await proc.WaitForExitAsync();
        return proc.ExitCode == 0 ? $"PASS\n{output}" : $"FAIL\n{output}{error}";
    }
}
