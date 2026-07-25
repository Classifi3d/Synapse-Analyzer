namespace Infrastructure.Configuration;

public class MinioOptions
{
    public const string SectionName = "MinIO";

    /// <summary>
    /// Endpoint the API and the Zeek service use to reach MinIO, e.g. http://localhost:9000
    /// locally or http://minio:9000 inside a compose network.
    /// </summary>
    public string Endpoint { get; set; } = "http://localhost:9000";

    /// <summary>
    /// Endpoint the browser uses. Presigned urls are signed against a specific host, so upload
    /// urls must be generated for the address the client can actually reach - rewriting the
    /// host afterwards would invalidate the signature. Defaults to <see cref="Endpoint"/>.
    /// </summary>
    public string? PublicEndpoint { get; set; }

    public string AccessKey { get; set; } = string.Empty;

    public string SecretKey { get; set; } = string.Empty;

    public string BucketName { get; set; } = "synapse-pcap-uploads";

    /// <summary>MinIO ignores the region, but SigV4 signing requires a value.</summary>
    public string Region { get; set; } = "us-east-1";

    /// <summary>
    /// Chunk size for multipart uploads. S3 requires at least 5 MiB for every part except the
    /// last. Default 64 MiB, which keeps the part count low for multi-gigabyte captures.
    /// </summary>
    public long PartSizeBytes { get; set; } = 64L * 1024 * 1024;

    /// <summary>Creates the bucket on startup when it does not already exist.</summary>
    public bool CreateBucketIfMissing { get; set; } = true;
}
