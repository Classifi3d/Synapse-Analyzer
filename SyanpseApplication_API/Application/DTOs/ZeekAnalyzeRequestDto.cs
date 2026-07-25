namespace Application.DTOs;

/// <summary>
/// Request contract of the FastAPI Zeek service. Carries coordinates only, never bytes.
/// Serialized with a snake_case policy to match the Python side.
/// </summary>
public class ZeekAnalyzeRequestDto
{
    public Guid AnalysisId { get; set; }

    public string BucketName { get; set; } = null!;

    public string ObjectKey { get; set; } = null!;

    /// <summary>
    /// Presigned GET url. Lets the Zeek service pull the capture without holding storage
    /// credentials of its own.
    /// </summary>
    public string DownloadUrl { get; set; } = null!;
}
