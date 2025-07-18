using System.Diagnostics;
using System.IO;
using System.Collections.Generic;
using Agent.Runtime.Tools;

namespace Codex.Plugin;

public class CodexTool : ITool
{
    public string Name => "codex";

    public async Task<string> ExecuteAsync(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return "No codex command provided.";

        var parts = input.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        var command = parts[0].ToLowerInvariant();
        var arg = parts.Length > 1 ? parts[1] : string.Empty;

        return command switch
        {
            "status" => RunGit("status --short"),
            "branch" => RunGit("branch --show-current"),
            "cat" => await CatAsync(arg),
            "ls" => Ls(arg),
            "diff" => RunGit($"diff {arg}"),
            "patch" => ApplyPatch(arg),
            "generate" => await GeneratePatchWithFilesAsync(arg),
            "autopatch" => await GenerateAndApplyPatchWithFilesAsync(arg),
            "annotate" => await AnnotateFileAsync(arg),
            "build" => BuildProject(arg),
            "run" => RunProject(arg),
            "test" => TestProject(arg),
            "tools" => ListTools(),
            _ => $"Unknown codex command: {command}"
        };
    }

    private static string RunGit(string arguments)
    {
        var psi = new ProcessStartInfo("git", arguments)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        psi.EnvironmentVariables["GIT_AUTHOR_NAME"] = "Codex";
        psi.EnvironmentVariables["GIT_AUTHOR_EMAIL"] = "codex@example.com";
        psi.EnvironmentVariables["GIT_COMMITTER_NAME"] = "Codex";
        psi.EnvironmentVariables["GIT_COMMITTER_EMAIL"] = "codex@example.com";
        try
        {
            using var proc = Process.Start(psi);
            if (proc == null)
                return "Failed to start git";
            proc.WaitForExit(5000);
            var output = proc.StandardOutput.ReadToEnd();
            var error = proc.StandardError.ReadToEnd();
            return string.IsNullOrWhiteSpace(output) ? error : output;
        }
        catch (Exception ex)
        {
            return $"git failed: {ex.Message}";
        }
    }

    private static async Task<string> CatAsync(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return "No file specified";

        if (!File.Exists(path))
            return $"File not found: {path}";

        return await File.ReadAllTextAsync(path);
    }

    private static string Ls(string path)
    {
        path = string.IsNullOrWhiteSpace(path) ? "." : path;
        if (!Directory.Exists(path))
            return $"Directory not found: {path}";

        var entries = Directory.GetFileSystemEntries(path)
            .Select(e => Path.GetFileName(e));
        return string.Join("\n", entries);
    }

    private static string ApplyPatch(string patchFile)
    {
        if (string.IsNullOrWhiteSpace(patchFile) || !File.Exists(patchFile))
            return $"Patch file not found: {patchFile}";

        return RunGit($"apply \"{patchFile}\"");
    }

    private static (string instruction, List<string> files, string? commit) ParseArgs(string arg)
    {
        var tokens = Tokenize(arg);
        var files = new List<string>();
        string? commit = null;
        for (int i = 0; i < tokens.Count; i++)
        {
            if (tokens[i] == "--files")
            {
                tokens.RemoveAt(i);
                while (i < tokens.Count && !tokens[i].StartsWith("--"))
                {
                    files.Add(tokens[i]);
                    tokens.RemoveAt(i);
                }
                i--;
            }
            else if (tokens[i] == "--commit")
            {
                tokens.RemoveAt(i);
                if (i < tokens.Count)
                {
                    commit = tokens[i];
                    tokens.RemoveAt(i);
                }
                i--;
            }
        }

        var instruction = string.Join(' ', tokens).Trim();
        return (instruction, files, commit);
    }

    private static List<string> Tokenize(string input)
    {
        var tokens = new List<string>();
        var current = new System.Text.StringBuilder();
        bool inQuote = false;
        foreach (var ch in input)
        {
            if (ch == '"')
            {
                inQuote = !inQuote;
                continue;
            }
            if (char.IsWhiteSpace(ch) && !inQuote)
            {
                if (current.Length > 0)
                {
                    tokens.Add(current.ToString());
                    current.Clear();
                }
            }
            else
            {
                current.Append(ch);
            }
        }
        if (current.Length > 0)
            tokens.Add(current.ToString());
        return tokens;
    }

    private static async Task<string> GeneratePatchAsync(string instruction, List<string> files)
    {
        if (string.IsNullOrWhiteSpace(instruction))
            return "No instruction provided";

        var provider = ToolRegistry.Provider;
        if (provider == null)
            return "LLM provider unavailable";

        var context = string.Empty;
        foreach (var file in files)
        {
            if (!File.Exists(file))
                continue;
            var text = await File.ReadAllTextAsync(file);
            if (text.Length > 2000)
                text = text.Substring(0, 2000);
            context += $"\nFile: {file}\n{text}";
        }

        var prompt = $"Generate a unified diff patch to implement the following instruction:\n{instruction}{context}";
        return await provider.CompleteAsync(prompt);
    }

    private static async Task<string> GeneratePatchWithFilesAsync(string arg)
    {
        var (instruction, files, _) = ParseArgs(arg);
        var patch = await GeneratePatchAsync(instruction, files);
        return patch;
    }

    private static async Task<string> GenerateAndApplyPatchWithFilesAsync(string arg)
    {
        var (instruction, files, commit) = ParseArgs(arg);
        var patch = await GeneratePatchAsync(instruction, files);
        if (string.IsNullOrWhiteSpace(patch) || patch.StartsWith("LLM provider") || patch.StartsWith("No instruction"))
            return patch;

        var psi = new ProcessStartInfo("git", "apply --index -")
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };

        try
        {
            using var proc = Process.Start(psi);
            if (proc == null)
                return "Failed to start git";
            await proc.StandardInput.WriteAsync(patch);
            proc.StandardInput.Close();
            proc.WaitForExit(5000);
            var error = await proc.StandardError.ReadToEndAsync();
            if (proc.ExitCode != 0)
                return string.IsNullOrWhiteSpace(error) ? "Failed to apply patch" : error;
            if (!string.IsNullOrWhiteSpace(commit))
            {
                RunGit($"-c user.email=codex@example.com -c user.name=Codex commit -am \"{commit.Replace("\"", "\\\"")}\"");
            }
            return patch;
        }
        catch (Exception ex)
        {
            return $"git apply failed: {ex.Message}";
        }
    }

    private static async Task<string> AnnotateFileAsync(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return $"File not found: {path}";

        var provider = ToolRegistry.Provider;
        if (provider == null)
            return "LLM provider unavailable";

        var text = await File.ReadAllTextAsync(path);
        if (text.Length > 4000)
            text = text.Substring(0, 4000);

        var prompt = $"Summarize the following file in a few sentences:\n{text}";
        return await provider.CompleteAsync(prompt);
    }

    private static string RunDotnet(string arguments)
    {
        var psi = new ProcessStartInfo("dotnet", arguments)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        try
        {
            using var proc = Process.Start(psi);
            if (proc == null)
                return "Failed to start dotnet";
            proc.WaitForExit(10000);
            var output = proc.StandardOutput.ReadToEnd();
            var error = proc.StandardError.ReadToEnd();
            return string.IsNullOrWhiteSpace(output) ? error : output;
        }
        catch (Exception ex)
        {
            return $"dotnet failed: {ex.Message}";
        }
    }

    private static string BuildProject(string arg)
    {
        var args = string.IsNullOrWhiteSpace(arg) ? "build" : $"build \"{arg}\"";
        return RunDotnet(args);
    }

    private static string RunProject(string arg)
    {
        var args = string.IsNullOrWhiteSpace(arg)
            ? "run"
            : $"run --project \"{arg}\"";
        return RunDotnet(args);
    }

    private static string TestProject(string arg)
    {
        var args = string.IsNullOrWhiteSpace(arg)
            ? "test -v minimal"
            : $"test \"{arg}\" -v minimal";
        return RunDotnet(args);
    }

    private static string ListTools()
    {
        var names = ToolRegistry.GetToolNames();
        return string.Join("\n", names);
    }
}
