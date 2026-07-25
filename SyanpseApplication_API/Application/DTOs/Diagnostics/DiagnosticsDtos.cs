namespace Application.DTOs.Diagnostics;

public class StoredObjectDto
{
    public string ObjectKey { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public DateTime? LastModified { get; set; }
    public string? ETag { get; set; }

    /// <summary>Ready-made url for fetching this object back through the diagnostics endpoint.</summary>
    public string? DownloadPath { get; set; }
}

public class UploadedObjectDto
{
    public string BucketName { get; set; } = string.Empty;
    public string ObjectKey { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public string? ETag { get; set; }
    public string? DownloadPath { get; set; }
}

/// <summary>Points the Zeek service at an object that is already in storage.</summary>
public class ZeekDiagnosticsRequestDto
{
    /// <summary>Key of the capture. Required.</summary>
    public string ObjectKey { get; set; } = null!;

    /// <summary>Defaults to the configured capture bucket.</summary>
    public string? BucketName { get; set; }
}

public class ComponentHealthDto
{
    public string Component { get; set; } = string.Empty;
    public bool Healthy { get; set; }
    public string Status { get; set; } = string.Empty;
    public long LatencyMs { get; set; }

    /// <summary>Component-specific detail: bucket list, model list, Zeek's own health payload.</summary>
    public object? Details { get; set; }

    public string? Error { get; set; }
}

public class SystemHealthDto
{
    public bool Healthy { get; set; }
    public DateTime CheckedAt { get; set; }
    public List<ComponentHealthDto> Components { get; set; } = [];
}
