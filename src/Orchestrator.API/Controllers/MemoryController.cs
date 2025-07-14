using Microsoft.AspNetCore.Mvc;
using Shared.Messaging;

namespace Orchestrator.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MemoryController : ControllerBase
{
    [HttpPost("{id}")]
    public IActionResult Send(string id, [FromBody] string entry)
    {
        MemoryHub.Send(id, entry);
        return Ok();
    }

    [HttpGet("{id}")]
    public IActionResult Receive(string id)
    {
        var entries = MemoryHub.Receive(id);
        return Ok(entries);
    }
}
