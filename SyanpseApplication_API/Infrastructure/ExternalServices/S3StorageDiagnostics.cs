using Amazon.S3;
using Amazon.S3.Model;
using Application.DTOs.Diagnostics;
using Application.Interfaces;
using Infrastructure.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Infrastructure.ExternalServices;

public class S3StorageDiagnostics(
    IAmazonS3 s3,
    IOptions<MinioOptions> options,
    ILogger<S3StorageDiagnostics> logger) : IStorageDiagnostics
{
    private const string KeyPrefix = "diagnostics/";

    private readonly MinioOptions _options = options.Value;

    public async Task<UploadedObjectDto> UploadAsync(
        Stream content,
        string fileName,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        // Namespaced away from captures/ so test objects are trivial to spot and purge.
        var objectKey = $"{KeyPrefix}{Guid.NewGuid()}/{SanitizeFileName(fileName)}";

        var response = await s3.PutObjectAsync(
            new PutObjectRequest
            {
                BucketName = _options.BucketName,
                Key = objectKey,
                InputStream = content,
                ContentType = contentType,
                AutoCloseStream = false
            },
            cancellationToken);

        logger.LogInformation("Diagnostics upload stored at {ObjectKey}.", objectKey);

        var size = await GetSizeAsync(objectKey, cancellationToken);

        return new UploadedObjectDto
        {
            BucketName = _options.BucketName,
            ObjectKey = objectKey,
            SizeBytes = size ?? 0,
            ETag = response.ETag,
            DownloadPath = BuildDownloadPath(objectKey)
        };
    }

    public async Task<(Stream Content, string ContentType, long Length)?> DownloadAsync(
        string objectKey,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await s3.GetObjectAsync(
                new GetObjectRequest
                {
                    BucketName = _options.BucketName,
                    Key = objectKey
                },
                cancellationToken);

            var contentType = string.IsNullOrWhiteSpace(response.Headers.ContentType)
                ? "application/octet-stream"
                : response.Headers.ContentType;

            return (response.ResponseStream, contentType, response.ContentLength);
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<IReadOnlyList<StoredObjectDto>> ListAsync(
        string? prefix,
        int maxKeys,
        CancellationToken cancellationToken = default)
    {
        var response = await s3.ListObjectsV2Async(
            new ListObjectsV2Request
            {
                BucketName = _options.BucketName,
                Prefix = prefix,
                MaxKeys = maxKeys
            },
            cancellationToken);

        return (response.S3Objects ?? [])
            .Select(o => new StoredObjectDto
            {
                ObjectKey = o.Key,
                SizeBytes = o.Size ?? 0,
                LastModified = o.LastModified,
                ETag = o.ETag,
                DownloadPath = BuildDownloadPath(o.Key)
            })
            .ToList();
    }

    public async Task<bool> DeleteAsync(
        string objectKey,
        CancellationToken cancellationToken = default)
    {
        // S3 delete succeeds whether or not the key existed, so check first to give the
        // caller a meaningful answer.
        if (await GetSizeAsync(objectKey, cancellationToken) is null)
            return false;

        await s3.DeleteObjectAsync(
            new DeleteObjectRequest
            {
                BucketName = _options.BucketName,
                Key = objectKey
            },
            cancellationToken);

        return true;
    }

    private async Task<long?> GetSizeAsync(string objectKey, CancellationToken cancellationToken)
    {
        try
        {
            var metadata = await s3.GetObjectMetadataAsync(
                new GetObjectMetadataRequest
                {
                    BucketName = _options.BucketName,
                    Key = objectKey
                },
                cancellationToken);

            return metadata.ContentLength;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    private static string BuildDownloadPath(string objectKey) =>
        $"/api/diagnostics/storage/objects/{Uri.EscapeDataString(objectKey)}";

    private static string SanitizeFileName(string fileName) =>
        new(Path.GetFileName(fileName)
            .Select(c => char.IsLetterOrDigit(c) || c is '.' or '-' or '_' ? c : '_')
            .ToArray());
}
