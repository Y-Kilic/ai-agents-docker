using Shared.Models;
using Agent.Runtime.Logging;

var config = new AgentConfig("runtime");
var logger = new CompositeAgentLogger(
    new ConsoleAgentLogger(),
    new HttpAgentLogger(config.Name));

await logger.LogAsync($"Starting agent: {config.Name}");

for (var i = 0; i < 3; i++)
{
    await logger.LogAsync($"Agent step {i}");
    await Task.Delay(1000);
}
