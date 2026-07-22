namespace Application.DTOs;

public class PcapUploadResultDto
{
    public Guid AnalysisId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime UploadedAt { get; set; }
}
