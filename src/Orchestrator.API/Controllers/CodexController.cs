using Microsoft.AspNetCore.Mvc;
using Orchestrator.API.Services;

namespace Orchestrator.API.Controllers;

public record CodexRequest(string Command);

[ApiController]
[Route("api/[controller]")]
public class CodexController : ControllerBase
{
    private readonly CodexService _codex;

    public CodexController(CodexService codex) => _codex = codex;

    [HttpPost("run")]
    public async Task<IActionResult> Run([FromBody] CodexRequest req)
    {
        var result = await _codex.RunAsync(req.Command);
        return Ok(result);
    }

    [HttpGet("status")]
    public ActionResult<string> Status() => Ok(_codex.Status);

    [HttpGet("logs")]
    public ActionResult<IReadOnlyList<string>> Logs() => Ok(_codex.GetLogs());

    [HttpDelete("logs")]
    public IActionResult ClearLogs()
    {
        _codex.ClearLogs();
        return NoContent();
    }
}
