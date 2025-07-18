using Microsoft.AspNetCore.Mvc;
using Orchestrator.API.Services;
using Shared.Models;

namespace Orchestrator.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class VmController : ControllerBase
{
    private readonly AgentOrchestrator _orchestrator;
    public VmController(AgentOrchestrator orchestrator)
    {
        _orchestrator = orchestrator;
    }

    [HttpGet("status")]
    public ActionResult<VmStatus> Status() => _orchestrator.GetVmStatus();

    [HttpPost("test")] 
    public async Task<IActionResult> Test()
    {
        await _orchestrator.LaunchTestVmAsync();
        return Ok();
    }
}
