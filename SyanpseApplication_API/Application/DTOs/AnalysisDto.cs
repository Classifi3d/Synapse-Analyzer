namespace Application.DTOs;

public class AnalysisDto
{
    public Guid AnalysisId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? UploadedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    /// <summary>The analyst request stored with the upload.</summary>
    public string? Prompt { get; set; }

    public bool? IsThreatDetected { get; set; }
    public string? Verdict { get; set; }
    public string? Report { get; set; }
    public string? ErrorMessage { get; set; }
    public ZeekSummaryDto? ZeekSummary { get; set; }
}
