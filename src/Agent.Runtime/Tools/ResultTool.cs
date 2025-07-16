using System.Threading.Tasks;

namespace Agent.Runtime.Tools;

public class ResultTool : ITool
{
    public string Name => "result";

    public Task<string> ExecuteAsync(string input)
    {
        return Task.FromResult(input.Trim());
    }
}
