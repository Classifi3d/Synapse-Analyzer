namespace Application.DTOs;

public class InitiateUploadResponseDto
{
    public Guid AnalysisId { get; set; }

    public string UploadId { get; set; } = null!;

    public string ObjectKey { get; set; } = null!;

    public List<PresignedUploadPartDto> Parts { get; set; } = [];
}