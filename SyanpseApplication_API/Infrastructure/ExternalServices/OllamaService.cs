//using Application.Interfaces;
//using Microsoft.Extensions.Configuration;
//using Microsoft.Extensions.Logging;
//using System.Net.Http.Json;
//using System.Text.Json;
//using System.Text.Json.Serialization;

//namespace Infrastructure.ExternalServices;

//public class OllamaService : IOllamaService
//{
//    private readonly HttpClient _httpClient;
//    private readonly ILogger<OllamaService> _logger;
//    private readonly string _modelName;

//    public OllamaService(
//        IConfiguration configuration, 
//        HttpClient httpClient, 
//        ILogger<OllamaService> logger)
//    {
//        _httpClient = httpClient;
//        _logger = logger;

//        var baseAddress = configuration["Ollama:ConnectionString"] ?? 
//            throw new InvalidOperationException("Ollama:ConnectionString is missing in appsettings.");
            
//        // Fixed: Assigned to the private field and corrected the double colon typo
//        _modelName = configuration["Ollama:Model"] ?? "llama3.1";

//        _httpClient.BaseAddress ??= new Uri(baseAddress);
//    }

//    public async Task<OllamaAnalysisResult> AnalyzePcapDataAsync(string prompt, string pcapData)
//    {
//        var systemPrompt = @"You are a cybersecurity expert analyzing PCAP packet data. 
//                               Analyze the provided logs for malicious patterns.
//                               Respond strictly with a JSON object: { ""isThreat"": boolean, ""details"": ""string"" }.";

//        var requestBody = new
//        {
//            model = _modelName,
//            prompt = $"{systemPrompt}\n\nUser Instructions:\n{prompt}\n\nPCAP Data:\n{pcapData}",
//            stream = false,
//            format = "json"
//        };

//        try
//        {
//            var response = await _httpClient.PostAsJsonAsync("api/generate", requestBody);
//            response.EnsureSuccessStatusCode();

//            var result = await response.Content.ReadFromJsonAsync<OllamaResponse>();
            
//            if (!string.IsNullOrWhiteSpace(result?.Response))
//            {
//                // Deserialize Ollama's JSON string into our strongly-typed application record
//                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
//                return JsonSerializer.Deserialize<OllamaAnalysisResult>(result.Response, options) 
//                       ?? new OllamaAnalysisResult { IsThreat = false, Details = "Failed to parse Ollama JSON." };
//            }

//            return new OllamaAnalysisResult { IsThreat = false, Details = "Empty response received." };
//        }
//        catch (Exception ex)
//        {
//            _logger.LogError(ex, "Error communicating with Ollama.");
//            return new OllamaAnalysisResult { IsThreat = false, Details = $"LLM Error: {ex.Message}" };
//        }
//    }

//    private class OllamaResponse
//    {
//        [JsonPropertyName("response")]
//        public string? Response { get; set; }
//    }
//}