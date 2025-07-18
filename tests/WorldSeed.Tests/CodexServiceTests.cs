using Orchestrator.API.Services;
using Shared.LLM;
using Xunit;

namespace WorldSeed.Tests;

public class CodexServiceTests
{
    [Fact]
    public async Task RunAsync_RecordsLogs()
    {
        var service = new CodexService(new MockOpenAIProvider("resp"));
        await service.RunAsync("generate change");
        var logs = service.GetLogs();
        Assert.Contains("> generate change", logs.First());
        Assert.Contains("resp", logs.Last());
    }

    [Fact]
    public void ClearLogs_RemovesEntries()
    {
        var service = new CodexService(new MockOpenAIProvider("resp"));
        service.RunAsync("echo hi").Wait();
        Assert.NotEmpty(service.GetLogs());
        service.ClearLogs();
        Assert.Empty(service.GetLogs());
    }
}

