using Microsoft.AspNetCore.Mvc;
using Shared.Messaging;

namespace Orchestrator.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MessageController : ControllerBase
{
    [HttpPost("{id}")]
    public IActionResult Send(string id, [FromBody] string message)
    {
        MessageHub.Send(id, message);
        return Ok();
    }

    [HttpGet("{id}")]
    public IActionResult Receive(string id)
    {
        var msgs = MessageHub.Receive(id);
        return Ok(msgs);
    }
}
