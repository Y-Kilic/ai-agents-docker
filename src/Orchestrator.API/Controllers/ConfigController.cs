using Microsoft.AspNetCore.Mvc;
using Orchestrator.API.Services;
using Shared.Models;

namespace Orchestrator.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ConfigController : ControllerBase
{
    private readonly AgentOrchestrator _orchestrator;

    public ConfigController(AgentOrchestrator orchestrator)
    {
        _orchestrator = orchestrator;
    }

    [HttpGet("llm")]
    public ActionResult<LLMConfig> GetLLM() => _orchestrator.GetLLMConfig();

    [HttpPost("llm")]
    public IActionResult SetLLM([FromBody] LLMConfig config)
    {
        _orchestrator.SetLLMConfig(config);
        return Ok();
    }
}
