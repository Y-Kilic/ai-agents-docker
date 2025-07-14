using Microsoft.AspNetCore.Mvc;
using Orchestrator.API.Logging;
using Shared.Models;

namespace Orchestrator.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AgentController : ControllerBase
{
    private readonly IAgentLogStore _logStore;

    public AgentController(IAgentLogStore logStore)
    {
        _logStore = logStore;
    }

    [HttpGet]
    public IActionResult GetDefaultConfig() => Ok(new AgentConfig("default"));

    [HttpPost("{id}/logs")]
    public IActionResult AddLog(string id, [FromBody] AgentLogEntry entry)
    {
        _logStore.Add(id, entry.Message);
        return Accepted();
    }

    [HttpGet("{id}/logs")]
    public IActionResult GetLogs(string id)
    {
        var logs = _logStore.Get(id);
        return Ok(logs);
    }
}

public record AgentLogEntry(string Message);
