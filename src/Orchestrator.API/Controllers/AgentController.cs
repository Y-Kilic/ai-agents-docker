using Microsoft.AspNetCore.Mvc;
using Orchestrator.API.Services;
using Shared.Models;

namespace Orchestrator.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AgentController : ControllerBase
{
    private readonly AgentOrchestrator _orchestrator;

    public AgentController(AgentOrchestrator orchestrator)
    {
        _orchestrator = orchestrator;
    }
    [HttpGet]
    public IActionResult GetConfig([FromQuery] AgentType type = AgentType.Default)
    {
        if (!AgentProfiles.TryGetProfile(type, out var config))
        {
            return NotFound();
        }

        return Ok(config);
    }

    [HttpPost("start")]
    public async Task<IActionResult> Start([FromBody] StartAgentRequest request)
    {
        var id = await _orchestrator.StartAgentAsync(request.Goal, request.Type);
        return Ok(new { id });
    }
}
