using System.Net.Http.Json;
using System.Text.Json;

namespace Agent.Runtime.Logging;

public class HttpAgentLogger : IAgentLogger
{
    private readonly HttpClient _client;
    private readonly string _agentId;

    public HttpAgentLogger(string agentId, HttpClient? client = null)
    {
        _client = client ?? new HttpClient();
        _agentId = agentId;
    }

    public async Task LogAsync(string message)
    {
        var payload = JsonSerializer.Serialize(new { message });
        await _client.PostAsync($"http://localhost:5000/api/agent/{_agentId}/logs",
            new StringContent(payload, System.Text.Encoding.UTF8, "application/json"));
    }
}
