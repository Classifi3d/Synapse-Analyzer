using Application.DTOs;

namespace Application.Interfaces;

public interface IThreatAnalysisService
{
    Task<InitiateUploadResponseDto> InitiateUploadAsync(
        Guid userId,
        InitiateUploadRequestDto request,
        CancellationToken cancellationToken = default);

    Task<AnalysisDto> CompleteUploadAsync(
        Guid userId,
        CompleteUploadRequestDto request,
        CancellationToken cancellationToken = default);

    Task<AnalysisDto> GetAnalysisAsync(
        Guid userId,
        Guid analysisId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AnalysisDto>> ListAnalysesAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Runs the full pipeline - Zeek analysis, prompt construction, LLM generation - emitting
    /// events as they occur. The report is persisted once generation finishes.
    /// </summary>
    IAsyncEnumerable<AnalysisStreamEvent> StreamAnalysisAsync(
        Guid userId,
        Guid analysisId,
        string? prompt,
        CancellationToken cancellationToken = default);
}
