namespace Application.DTOs;

public class ThreatAnalysisResultDto
{
    public Guid AnalysisId { get; set; }
    public string Status { get; set; } = string.Empty;
    public bool IsThreatDetected { get; set; }
    public string Verdict { get; set; } = string.Empty;
    public string AnalysisDetails { get; set; } = string.Empty;
    public DateTime CompletedAt { get; set; }
    public ZeekSummaryDto ZeekSummary { get; set; } = new();
}