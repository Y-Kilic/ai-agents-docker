using Agent.Runtime.Tools;
using Codex.Plugin;
using Shared.LLM;

namespace Orchestrator.API.Services;

public class CodexService
{
    private readonly CodexTool _tool = new();
    private readonly List<string> _logs = new();

    public string Status { get; private set; } = "Idle";

    public CodexService(ILLMProvider provider)
    {
        ToolRegistry.Initialize(provider, new List<string>(), Log);
    }

    private void Log(string message)
    {
        lock (_logs)
        {
            _logs.Add(message);
            if (_logs.Count > 100)
                _logs.RemoveAt(0);
        }
    }

    public IReadOnlyList<string> GetLogs()
    {
        lock (_logs)
            return _logs.ToList();
    }

    public void ClearLogs()
    {
        lock (_logs)
            _logs.Clear();
    }

    public async Task<string> RunAsync(string command)
    {
        Status = $"Running: {command}";
        Log($"> {command}");
        var result = await _tool.ExecuteAsync(command);
        Log(result.Trim());
        Status = "Idle";
        return result;
    }
}
