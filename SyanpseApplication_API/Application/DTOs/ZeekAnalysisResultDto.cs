namespace Application.DTOs;

public class ZeekAnalysisResultDto
{
    public bool Success { get; set; }

    /// <summary>Populated by the Zeek service when <see cref="Success"/> is false.</summary>
    public string? Error { get; set; }

    public double DurationSeconds { get; set; }

    public ZeekSummaryDto Summary { get; set; } = new();

    public ZeekLogsDto Logs { get; set; } = new();

    /// <summary>Log names whose rows were sampled rather than returned in full.</summary>
    public List<string> TruncatedLogs { get; set; } = [];
}
