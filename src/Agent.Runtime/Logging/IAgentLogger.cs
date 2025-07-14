namespace Agent.Runtime.Logging;

public interface IAgentLogger
{
    Task LogAsync(string message);
}
