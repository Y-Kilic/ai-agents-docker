namespace Agent.Runtime.Tools;

public class EchoTool : ITool
{
    public string Name => "echo";

    public Task<string> ExecuteAsync(string input)
    {
        return Task.FromResult($"Echo: {input}");
    }
}
