namespace Domain.Entities;

public class ThreatAnalysisResult
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty; // Now stores the MinIO Object Name/Path
    public long FileSizeBytes { get; set; }
    public DateTime UploadedAt { get; set; }
    public string Status { get; set; } = "Pending"; // Pending, Processing, Completed, Failed
    public bool? IsThreatDetected { get; set; }
    public string? AnalysisDetails { get; set; }

    // Navigation property
    public User User { get; set; } = null!;
}

