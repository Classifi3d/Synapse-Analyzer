namespace Application.DTOs;

/// <summary>
/// One SSE frame. <see cref="Event"/> becomes the SSE event name so the client can tell a
/// progress update apart from a generated token without parsing the payload.
/// </summary>
public sealed record AnalysisStreamEvent(string Event, object? Data)
{
    public static AnalysisStreamEvent Status(string stage, string message) =>
        new("status", new { stage, message });

    public static AnalysisStreamEvent Summary(ZeekSummaryDto summary) =>
        new("summary", summary);

    public static AnalysisStreamEvent Token(string text) =>
        new("token", new { text });

    public static AnalysisStreamEvent Done(Guid analysisId, bool isThreatDetected, string? verdict) =>
        new("done", new { analysisId, isThreatDetected, verdict });

    public static AnalysisStreamEvent Error(string message) =>
        new("error", new { message });
}
