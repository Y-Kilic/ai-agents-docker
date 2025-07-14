namespace Agent.Runtime.Logging;

public class ConsoleAgentLogger : IAgentLogger
{
    public Task LogAsync(string message)
    {
        Console.WriteLine(message);
        return Task.CompletedTask;
    }
}
