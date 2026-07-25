using Application.DTOs;

namespace Application.Interfaces;

public interface IZeekProcessor
{
    /// <summary>
    /// Asks the Zeek service to analyze a capture. Only coordinates are sent; the service
    /// fetches the object from storage itself.
    /// </summary>
    Task<ZeekAnalysisResultDto> AnalyzeAsync(
        Guid analysisId,
        string bucketName,
        string objectKey,
        string downloadUrl,
        CancellationToken cancellationToken = default);
}
