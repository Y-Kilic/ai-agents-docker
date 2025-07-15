using Microsoft.AspNetCore.Mvc;
using Orchestrator.API.Services;

namespace Orchestrator.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MessageController : ControllerBase
{
    private readonly AgentOrchestrator _orchestrator;

    public MessageController(AgentOrchestrator orchestrator)
    {
        _orchestrator = orchestrator;
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> Receive(string id)
    {
        var msgs = await _orchestrator.GetMessagesAsync(id);
        return Ok(msgs);
    }

    [HttpGet("{id}/all")]
    public async Task<IActionResult> ReceiveAll(string id)
    {
        var msgs = await _orchestrator.GetAllMessagesAsync(id);
        return Ok(msgs);
    }
}
