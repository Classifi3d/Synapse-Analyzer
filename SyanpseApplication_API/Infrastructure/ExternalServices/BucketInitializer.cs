using Amazon.S3;
using Amazon.S3.Model;
using Infrastructure.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Infrastructure.ExternalServices;

/// <summary>
/// Creates the capture bucket on startup if it is missing, so a fresh MinIO container works
/// without a manual setup step.
/// </summary>
/// <remarks>
/// CORS is not configured here: MinIO does not implement the S3 PutBucketCors API and instead
/// takes allowed origins from the MINIO_API_CORS_ALLOW_ORIGIN environment variable. Browser
/// uploads will fail until that is set on the MinIO container.
/// </remarks>
public class BucketInitializer(
    IAmazonS3 s3,
    IOptions<MinioOptions> options,
    ILogger<BucketInitializer> logger) : IHostedService
{
    private readonly MinioOptions _options = options.Value;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!_options.CreateBucketIfMissing)
            return;

        try
        {
            var buckets = await s3.ListBucketsAsync(cancellationToken);

            if (buckets.Buckets?.Any(b => b.BucketName == _options.BucketName) == true)
                return;

            await s3.PutBucketAsync(
                new PutBucketRequest { BucketName = _options.BucketName },
                cancellationToken);

            logger.LogInformation("Created MinIO bucket '{Bucket}'.", _options.BucketName);
        }
        catch (Exception ex)
        {
            // Startup continues: MinIO may simply not be up yet, and the bucket can also be
            // provisioned out of band.
            logger.LogWarning(
                ex,
                "Could not verify or create the MinIO bucket '{Bucket}'.",
                _options.BucketName);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
