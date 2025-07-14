namespace Agent.Runtime.Tools;

public interface ITool
{
    string Name { get; }
    Task<string> ExecuteAsync(string input);
}
