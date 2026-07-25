using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using Application.Exceptions;
using Application.Interfaces;
using Infrastructure.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Infrastructure.ExternalServices;

/// <summary>
/// Streaming client for Ollama's /api/generate endpoint. Receives a finished prompt - it has
/// no knowledge of Zeek, captures, or how the prompt was assembled.
/// </summary>
public class OllamaService : IOllamaService
{
    private readonly HttpClient _httpClient;
    private readonly OllamaOptions _options;
    private readonly ILogger<OllamaService> _logger;

    public OllamaService(
        HttpClient httpClient,
        IOptions<OllamaOptions> options,
        ILogger<OllamaService> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async IAsyncEnumerable<string> StreamAsync(
        string prompt,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var request = new OllamaGenerateRequest
        {
            Model = _options.Model,
            Prompt = prompt,
            Stream = true,
            Options = new OllamaModelOptions
            {
                NumCtx = _options.ContextLength,
                Temperature = _options.Temperature
            }
        };

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "api/generate")
        {
            Content = JsonContent.Create(request)
        };

        // Return as soon as headers arrive so tokens can be forwarded while the model is
        // still generating, rather than buffering the whole response.
        using var response = await _httpClient.SendAsync(
            httpRequest,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            throw new AnalysisPipelineException(
                $"Ollama returned {(int)response.StatusCode}: {body}");
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream);

        // Ollama emits newline-delimited JSON, one object per token.
        while (await reader.ReadLineAsync(cancellationToken) is { } line)
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            OllamaStreamChunk? chunk;

            try
            {
                chunk = JsonSerializer.Deserialize<OllamaStreamChunk>(line);
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Skipping malformed Ollama chunk: {Line}", line);
                continue;
            }

            if (chunk is null)
                continue;

            if (!string.IsNullOrEmpty(chunk.Error))
                throw new AnalysisPipelineException($"Ollama reported an error: {chunk.Error}");

            if (!string.IsNullOrEmpty(chunk.Response))
                yield return chunk.Response;

            if (chunk.Done)
                yield break;
        }
    }

    private sealed class OllamaGenerateRequest
    {
        [JsonPropertyName("model")]
        public string Model { get; set; } = default!;

        [JsonPropertyName("prompt")]
        public string Prompt { get; set; } = default!;

        [JsonPropertyName("stream")]
        public bool Stream { get; set; }

        [JsonPropertyName("options")]
        public OllamaModelOptions? Options { get; set; }
    }

    private sealed class OllamaModelOptions
    {
        [JsonPropertyName("num_ctx")]
        public int NumCtx { get; set; }

        [JsonPropertyName("temperature")]
        public double Temperature { get; set; }
    }

    private sealed class OllamaStreamChunk
    {
        [JsonPropertyName("response")]
        public string? Response { get; set; }

        [JsonPropertyName("done")]
        public bool Done { get; set; }

        [JsonPropertyName("error")]
        public string? Error { get; set; }
    }
}
