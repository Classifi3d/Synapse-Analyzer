using Application.DTOs;
using Application.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Infrastructure.ExternalServices;

public class OllamaService : IOllamaService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<OllamaService> _logger;
    private readonly string _modelName;

    public OllamaService(
        IConfiguration configuration,
        HttpClient httpClient,
        ILogger<OllamaService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;

        var baseAddress = configuration["Ollama:ConnectionString"]
            ?? throw new InvalidOperationException("Missing Ollama:ConnectionString.");

        _modelName = configuration["Ollama:Model"]
            ?? throw new InvalidOperationException("Missing Ollama:Model.");

        _httpClient.BaseAddress ??= new Uri(baseAddress);
    }

    public async IAsyncEnumerable<string> AnalyzeAsync(
        string prompt,
        ZeekAnalysisResultDto zeekResult,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var request = new OllamaGenerateRequest
        {
            Model = _modelName,
            Stream = true,
            Prompt = BuildPrompt(prompt, zeekResult)
        };

        using var response = await _httpClient.PostAsJsonAsync(
            "api/generate",
            request,
            cancellationToken);

        response.EnsureSuccessStatusCode();

        await using var stream =
            await response.Content.ReadAsStreamAsync(cancellationToken);

        using var reader = new StreamReader(stream);

        while (!reader.EndOfStream && !cancellationToken.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(cancellationToken);

            if (string.IsNullOrWhiteSpace(line))
                continue;

            OllamaStreamChunk? chunk;

            try
            {
                chunk = JsonSerializer.Deserialize<OllamaStreamChunk>(line);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to deserialize Ollama streaming response.");
                continue;
            }

            if (!string.IsNullOrWhiteSpace(chunk?.Response))
            {
                yield return chunk.Response;
            }

            if (chunk?.Done == true)
                yield break;
        }
    }

    private static string BuildPrompt(
        string userPrompt,
        ZeekAnalysisResultDto zeekResult)
    {
        var zeekJson = JsonSerializer.Serialize(
            zeekResult,
            new JsonSerializerOptions
            {
                WriteIndented = true
            });

        var builder = new StringBuilder();

        builder.AppendLine("""
You are an expert cybersecurity analyst.

Analyze the supplied Zeek analysis.

Look for:

- Malware
- Command & Control traffic
- Beaconing
- DNS tunneling
- Data exfiltration
- Lateral movement
- Port scans
- Malicious HTTP activity
- Suspicious TLS behaviour
- Indicators of compromise

Base every conclusion ONLY on the supplied Zeek data.

Produce a professional report using the following sections:

# Executive Summary

# Findings

# Indicators of Compromise

# Risk Assessment

# Recommendations
""");

        builder.AppendLine();
        builder.AppendLine("User Instructions:");
        builder.AppendLine(userPrompt);

        builder.AppendLine();
        builder.AppendLine("Zeek Analysis:");
        builder.AppendLine(zeekJson);

        return builder.ToString();
    }

    private sealed class OllamaGenerateRequest
    {
        [JsonPropertyName("model")]
        public string Model { get; set; } = default!;

        [JsonPropertyName("prompt")]
        public string Prompt { get; set; } = default!;

        [JsonPropertyName("stream")]
        public bool Stream { get; set; }
    }

    private sealed class OllamaStreamChunk
    {
        [JsonPropertyName("response")]
        public string? Response { get; set; }

        [JsonPropertyName("done")]
        public bool Done { get; set; }
    }
}