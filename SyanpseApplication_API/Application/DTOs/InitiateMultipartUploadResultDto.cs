namespace Application.DTOs;

public class InitiateMultipartUploadResultDto
{
    public string UploadId { get; set; } = null!;
    public string ObjectKey { get; set; } = null!;
    public List<PresignedUploadPartDto> Parts { get; set; } = [];
}