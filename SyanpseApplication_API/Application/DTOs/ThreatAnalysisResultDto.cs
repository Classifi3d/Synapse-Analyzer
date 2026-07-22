namespace Application.DTOs;

public class ThreatAnalysisResultDto
{
    public Guid Id { get; set; }
    public bool? IsThreatDetected { get; set; }
    public string? Details { get; set; }
}