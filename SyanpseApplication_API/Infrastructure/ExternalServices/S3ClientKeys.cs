namespace Infrastructure.ExternalServices;

public static class S3ClientKeys
{
    /// <summary>Keyed S3 client configured with the browser-facing endpoint.</summary>
    public const string Public = "minio-public";

    /// <summary>
    /// Keyed S3 client used only by health probes: no retries and a short timeout, so an
    /// unreachable MinIO is reported in seconds instead of after the default retry budget.
    /// </summary>
    public const string Probe = "minio-probe";
}
