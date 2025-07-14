using Microsoft.AspNetCore.Mvc;
using Shared.Models;

namespace Orchestrator.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AgentController : ControllerBase
{
    [HttpGet]
    public IActionResult GetDefaultConfig() => Ok(new AgentConfig("default"));
}
