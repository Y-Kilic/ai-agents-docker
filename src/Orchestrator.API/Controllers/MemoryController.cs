using Microsoft.AspNetCore.Mvc;
using Orchestrator.API.Services;

namespace Orchestrator.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MemoryController : ControllerBase
{
    private readonly AgentOrchestrator _orchestrator;

    public MemoryController(AgentOrchestrator orchestrator)
    {
        _orchestrator = orchestrator;
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> Receive(string id)
    {
        var entries = await _orchestrator.GetMemoryAsync(id);
        return Ok(entries);
    }
}
