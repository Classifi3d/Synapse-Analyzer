namespace Domain.Entities;

/// <summary>
/// Metadata for a single packet capture analysis. The capture itself never lives here -
/// only the coordinates needed to locate it in object storage.
/// </summary>
public class Analysis
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }

    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }

    /// <summary>Bucket holding the capture.</summary>
    public string BucketName { get; set; } = string.Empty;

    /// <summary>Key of the capture inside <see cref="BucketName"/>.</summary>
    public string ObjectKey { get; set; } = string.Empty;

    /// <summary>S3 multipart upload identifier; cleared once the upload is completed or aborted.</summary>
    public string? UploadId { get; set; }

    public AnalysisStatus Status { get; set; } = AnalysisStatus.AwaitingUpload;

    public DateTime CreatedAt { get; set; }
    public DateTime? UploadedAt { get; set; }
    public DateTime? AnalyzedAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// What the analyst asked for, submitted alongside the upload. Combined with the system
    /// instructions and the Zeek output when the prompt is assembled.
    /// </summary>
    public string? Prompt { get; set; }

    /// <summary>Structured Zeek output, cached so a repeated report request does not re-run Zeek.</summary>
    public string? ZeekResultJson { get; set; }

    public bool? IsThreatDetected { get; set; }

    /// <summary>Short verdict line derived from the generated report.</summary>
    public string? Verdict { get; set; }

    /// <summary>The full markdown report produced by the LLM.</summary>
    public string? Report { get; set; }

    public string? ErrorMessage { get; set; }

    public User User { get; set; } = null!;
}
