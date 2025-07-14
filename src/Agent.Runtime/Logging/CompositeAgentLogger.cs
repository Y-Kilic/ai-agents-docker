namespace Agent.Runtime.Logging;

public class CompositeAgentLogger : IAgentLogger
{
    private readonly IAgentLogger[] _loggers;

    public CompositeAgentLogger(params IAgentLogger[] loggers)
    {
        _loggers = loggers;
    }

    public async Task LogAsync(string message)
    {
        foreach (var logger in _loggers)
        {
            await logger.LogAsync(message);
        }
    }
}
