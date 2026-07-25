using System.Net.Http.Json;
using System.Text.Json;
using Application.DTOs;
using Application.Exceptions;
using Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace Infrastructure.ExternalServices;

/// <summary>
/// HTTP client for the FastAPI Zeek service. The capture never crosses this boundary - only
/// its coordinates and a presigned url the service uses to fetch it.
/// </summary>
public class ZeekProcessor : IZeekProcessor
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ZeekProcessor> _logger;

    /// <summary>Python side is snake_case; the DTOs stay idiomatic C# and are mapped here.</summary>
    private static readonly JsonSerializerOptions SnakeCase = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
        DictionaryKeyPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    public ZeekProcessor(HttpClient httpClient, ILogger<ZeekProcessor> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<ZeekAnalysisResultDto> AnalyzeAsync(
        Guid analysisId,
        string bucketName,
        string objectKey,
        string downloadUrl,
        CancellationToken cancellationToken = default)
    {
        var request = new ZeekAnalyzeRequestDto
        {
            AnalysisId = analysisId,
            BucketName = bucketName,
            ObjectKey = objectKey,
            DownloadUrl = downloadUrl
        };

        _logger.LogInformation(
            "Dispatching analysis {AnalysisId} to the Zeek service ({ObjectKey}).",
            analysisId,
            objectKey);

        using var response = await _httpClient.PostAsJsonAsync(
            "analyze",
            request,
            SnakeCase,
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            throw new AnalysisPipelineException(
                $"The Zeek service returned {(int)response.StatusCode}: {Truncate(body)}");
        }

        var result = await response.Content.ReadFromJsonAsync<ZeekAnalysisResultDto>(
            SnakeCase,
            cancellationToken);

        return result ?? throw new AnalysisPipelineException(
            "The Zeek service returned an empty response.");
    }

    private static string Truncate(string value) =>
        value.Length <= 500 ? value : value[..500] + "...";
}
