using System.ComponentModel.DataAnnotations;

namespace Application.DTOs;

public class CompleteUploadRequestDto
{
    [Required]
    public Guid AnalysisId { get; set; }

    /// <summary>
    /// The ETag returned by MinIO for every uploaded part. S3 cannot assemble the object
    /// without them, so the client must collect each PUT response's ETag header.
    /// </summary>
    [Required]
    [MinLength(1)]
    public List<CompletedPartDto> Parts { get; set; } = [];

    /// <summary>
    /// What the analyst wants done with the capture, submitted together with the upload.
    /// Optional - the stream endpoint can override it, and a general assessment is produced
    /// when neither is supplied.
    /// </summary>
    [StringLength(4000)]
    public string? Prompt { get; set; }
}
