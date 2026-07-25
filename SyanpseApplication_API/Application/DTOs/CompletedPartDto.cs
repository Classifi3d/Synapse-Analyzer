using System.ComponentModel.DataAnnotations;

namespace Application.DTOs;

public class CompletedPartDto
{
    [Range(1, 10_000)]
    public int PartNumber { get; set; }

    [Required]
    public string ETag { get; set; } = null!;
}
