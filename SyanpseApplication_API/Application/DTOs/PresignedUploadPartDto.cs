namespace Application.DTOs;

public class PresignedUploadPartDto
{
    public int PartNumber { get; set; }
    public string UploadUrl { get; set; } = null!;
}