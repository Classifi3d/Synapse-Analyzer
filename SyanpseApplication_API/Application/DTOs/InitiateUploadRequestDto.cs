using System.ComponentModel.DataAnnotations;

namespace Application.DTOs;

public class InitiateUploadRequestDto
{
    [Required]
    [StringLength(260, MinimumLength = 1)]
    public string FileName { get; set; } = null!;

    public string? ContentType { get; set; }

    [Range(1, long.MaxValue)]
    public long FileSize { get; set; }
}
