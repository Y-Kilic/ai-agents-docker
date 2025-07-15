namespace Shared.Models;

public record StartAgentRequest(string Goal, AgentType Type = AgentType.Default, int Loops = 5);
