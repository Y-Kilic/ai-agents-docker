using Microsoft.AspNetCore.Mvc;
using Shared.Models;

namespace Orchestrator.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AgentController : ControllerBase
{
    [HttpGet]
    public IActionResult GetConfig([FromQuery] AgentType type = AgentType.Default)
    {
        if (!AgentProfiles.TryGetProfile(type, out var config))
        {
            return NotFound();
        }

        return Ok(config);
    }
}
