namespace Application.DTOs;

public class InitiateUploadRequestDto
{
    public string FileName { get; set; } = null!;
    public string ContentType { get; set; } = null!;
    public long FileSize { get; set; }
}