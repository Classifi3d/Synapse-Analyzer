namespace Application.DTOs;

public class CompleteUploadRequestDto
{
    public Guid AnalysisId { get; set; }
    public string UploadId { get; set; } = null!;
    public string ObjectKey { get; set; } = null!;
}