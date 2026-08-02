namespace Infrastructure.Configuration;

public class MinioOptions
{
    public const string SectionName = "MinIO";

    /// <summary>
    /// Endpoint this API uses to reach MinIO directly, e.g. http://localhost:9000 when the
    /// API runs on the host or http://minio:9000 inside a compose network.
    /// </summary>
    public string Endpoint { get; set; } = "http://localhost:9000";

    /// <summary>
    /// Endpoint the browser uses. Presigned urls are signed against a specific host, so upload
    /// urls must be generated for the address the client can actually reach - rewriting the
    /// host afterwards would invalidate the signature. Defaults to <see cref="Endpoint"/>.
    /// </summary>
    public string? PublicEndpoint { get; set; }

    /// <summary>
    /// Endpoint the Zeek service uses to fetch a capture.
    /// </summary>
    /// <remarks>
    /// MinIO has three consumers reaching it from three different network positions - this
    /// API, the browser, and the Zeek container - and a presigned url is only valid for the
    /// host it was signed against. Each therefore needs its own endpoint.
    ///
    /// The Zeek service usually runs in a container while the API runs on the host, so
    /// `localhost` means two different machines to the two of them: a url signed for
    /// localhost:9000 resolves to the container itself and the download fails. Defaults to
    /// <see cref="Endpoint"/> for an all-in-one deployment where they agree.
    /// </remarks>
    public string? ZeekEndpoint { get; set; }

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
