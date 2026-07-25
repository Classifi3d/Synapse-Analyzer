using Application.DTOs;

namespace Application.Interfaces;

public interface IOllamaService
{
    IAsyncEnumerable<string> AnalyzeAsync(
        string prompt,
        ZeekAnalysisResultDto zeekResult,
        CancellationToken cancellationToken = default);
}