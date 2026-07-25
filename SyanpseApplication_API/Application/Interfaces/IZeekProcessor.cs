using Application.DTOs;

namespace Application.Interfaces;

public interface IZeekProcessor
{
    Task<ZeekAnalysisResultDto> AnalyzeAsync(
        Guid analysisId,
        string bucketName,
        string objectKey,
        CancellationToken cancellationToken = default);
}