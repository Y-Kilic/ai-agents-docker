using Microsoft.AspNetCore.Mvc;
using Orchestrator.API.Services;
using Shared.Models;

namespace Orchestrator.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class OverseerController : ControllerBase
{
    private readonly OverseerService _overseer;

    public OverseerController(OverseerService overseer)
    {
        _overseer = overseer;
    }

    [HttpPost("start")]
    public async Task<IActionResult> Start([FromBody] StartOverseerRequest request)
    {
        var id = await _overseer.StartAsync(request.Goal, request.Loops);
        return Ok(new { id });
    }

    [HttpPost("{id}/stop")]
    public async Task<IActionResult> Stop(string id)
    {
        await _overseer.StopAsync(id);
        return Ok();
    }

    [HttpGet("list")]
    public IActionResult List()
    {
        var result = _overseer.List();
        return Ok(result);
    }

    [HttpGet("{id}/status")]
    public async Task<IActionResult> Status(string id)
    {
        var status = await _overseer.GetStatusAsync(id);
        if (status == null)
            return NotFound();
        return Ok(status);
    }
}
