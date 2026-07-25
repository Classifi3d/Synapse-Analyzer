namespace Application.DTOs;

public class InitiateUploadResponseDto
{
    public Guid AnalysisId { get; set; }

    public string UploadId { get; set; } = null!;

    public string BucketName { get; set; } = null!;

    public string ObjectKey { get; set; } = null!;

    /// <summary>Exact chunk size the client must slice the file into (last part may be smaller).</summary>
    public long PartSizeBytes { get; set; }

    public DateTime ExpiresAtUtc { get; set; }

    public List<PresignedUploadPartDto> Parts { get; set; } = [];
}
