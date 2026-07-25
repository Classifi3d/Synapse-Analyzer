namespace Application.DTOs;

public class ZeekAnalyzeRequestDto
{
    public Guid AnalysisId { get; set; }
    public string BucketName { get; set; } = null!;
    public string ObjectKey { get; set; } = null!;
}