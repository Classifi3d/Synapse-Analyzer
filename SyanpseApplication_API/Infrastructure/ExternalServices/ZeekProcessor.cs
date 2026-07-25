using System.Net.Http.Json;
using Application.DTOs;
using Application.Interfaces;

namespace Infrastructure.ExternalServices;

public class ZeekProcessor : IZeekProcessor
{
    private readonly HttpClient _httpClient;

    public ZeekProcessor(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<ZeekAnalysisResultDto> AnalyzeAsync(
        Guid analysisId,
        string bucketName,
        string objectKey,
        CancellationToken cancellationToken = default)
    {
        var request = new ZeekAnalyzeRequestDto
        {
            AnalysisId = analysisId,
            BucketName = bucketName,
            ObjectKey = objectKey
        };

        using var response = await _httpClient.PostAsJsonAsync(
            "/analyze",
            request,
            cancellationToken);

        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<ZeekAnalysisResultDto>(
            cancellationToken: cancellationToken);

        return result ?? throw new InvalidOperationException(
            "The Zeek service returned an empty response.");
    }
}