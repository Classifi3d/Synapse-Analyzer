using Application.DTOs;

namespace Application.Interfaces;

public interface IThreatAnalysisService
{
    Task<InitiateUploadResponseDto> InitiateUploadAsync(
        Guid userId,
        InitiateUploadRequestDto request);

    Task CompleteUploadAsync(
        Guid userId,
        CompleteUploadRequestDto request);

    Task<ThreatAnalysisResultDto> ProcessAnalysisAsync(
        Guid userId,
        Guid analysisId,
        string prompt);

    IAsyncEnumerable<string> StreamAnalysisAsync(
        Guid userId,
        Guid analysisId);
}