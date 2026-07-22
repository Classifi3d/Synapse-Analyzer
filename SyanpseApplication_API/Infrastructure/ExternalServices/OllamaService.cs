using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Infrastructure.ExternalServices;

public class OllamaService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<OllamaService> _logger;
    private readonly string _modelName = "llama3.1"; 

    public OllamaService(HttpClient httpClient, ILogger<OllamaService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<string?> AnalyzePcapDataAsync(string parsedPcapJson)
    {
        var systemPrompt = @"You are a cybersecurity expert analyzing PCAP packet data. 
                               Analyze the provided JSON packet summary for malicious patterns.
                               Respond with a JSON object: { 'isThreat': boolean, 'details': string }.";

        var requestBody = new
        {
            model = _modelName,
            prompt = $"{systemPrompt}\n\nPCAP Data:\n{parsedPcapJson}",
            stream = false
        };

        try
        {
            var response = await _httpClient.PostAsJsonAsync("http://localhost:11434/api/generate", requestBody);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<OllamaResponse>();
            return result?.Response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error communicating with Ollama.");
            return null;
        }
    }

    private class OllamaResponse
    {
        public string? Response { get; set; }
    }
}

