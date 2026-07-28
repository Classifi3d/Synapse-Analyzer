using Amazon.S3;
using Amazon.S3.Model;
using Application.Interfaces;
using Application.Models;
using Infrastructure.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Infrastructure.ExternalServices;

/// <summary>
/// MinIO adapter. Two clients are used deliberately: presigned urls are signed against a
/// specific host, so upload urls must be signed for the address the browser can reach while
/// internal operations use the address the API can reach. In a single-host setup both point
/// at the same endpoint.
/// </summary>
public class S3FileStorageService : IFileStorageService
{
    private readonly IAmazonS3 _internalClient;
    private readonly IAmazonS3 _publicClient;
    private readonly MinioOptions _options;
    private readonly ILogger<S3FileStorageService> _logger;

    public S3FileStorageService(
        IAmazonS3 internalClient,
        [FromKeyedServices(S3ClientKeys.Public)] IAmazonS3 publicClient,
        IOptions<MinioOptions> options,
        ILogger<S3FileStorageService> logger)
    {
        _internalClient = internalClient;
        _publicClient = publicClient;
        _options = options.Value;
        _logger = logger;
    }

    public string BucketName => _options.BucketName;

    public long PartSizeBytes => _options.PartSizeBytes;

    public async Task<MultipartUploadSession> InitiateMultipartUploadAsync(
        string objectKey,
        string contentType,
        int partCount,
        CancellationToken cancellationToken = default)
    {
        var initiateResponse = await _internalClient.InitiateMultipartUploadAsync(
            new InitiateMultipartUploadRequest
            {
                BucketName = _options.BucketName,
                Key = objectKey,
                ContentType = contentType
            },
            cancellationToken);

        var expiresAt = DateTime.UtcNow.AddHours(6);
        var parts = new List<PresignedPart>(partCount);

        for (var partNumber = 1; partNumber <= partCount; partNumber++)
        {
            // No ContentType here: the browser must not send one on part PUTs, or the
            // signature will not match.
            var url = await _publicClient.GetPreSignedURLAsync(new GetPreSignedUrlRequest
            {
                BucketName = _options.BucketName,
                Key = objectKey,
                Verb = HttpVerb.PUT,
                Expires = expiresAt,
                UploadId = initiateResponse.UploadId,
                PartNumber = partNumber,
                Protocol = ProtocolFor(PublicEndpoint)
            });

            parts.Add(new PresignedPart(partNumber, url));
        }

        return new MultipartUploadSession(initiateResponse.UploadId, parts, expiresAt);
    }

    public async Task<long> CompleteMultipartUploadAsync(
        string objectKey,
        string uploadId,
        IReadOnlyList<CompletedPart> parts,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await _internalClient.CompleteMultipartUploadAsync(
                new CompleteMultipartUploadRequest
                {
                    BucketName = _options.BucketName,
                    Key = objectKey,
                    UploadId = uploadId,
                    PartETags = parts
                        .Select(p => new PartETag(p.PartNumber, NormalizeETag(p.ETag)))
                        .ToList()
                },
                cancellationToken);
        }
        catch (AmazonS3Exception ex)
        {
            _logger.LogError(
                ex,
                "CompleteMultipartUpload failed for {ObjectKey} (upload {UploadId}).",
                objectKey,
                uploadId);

            throw;
        }

        var size = await GetObjectSizeAsync(objectKey, cancellationToken);

        return size ?? 0;
    }

    public async Task AbortMultipartUploadAsync(
        string objectKey,
        string uploadId,
        CancellationToken cancellationToken = default)
    {
        await _internalClient.AbortMultipartUploadAsync(
            new AbortMultipartUploadRequest
            {
                BucketName = _options.BucketName,
                Key = objectKey,
                UploadId = uploadId
            },
            cancellationToken);
    }

    public async Task<long?> GetObjectSizeAsync(
        string objectKey,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var metadata = await _internalClient.GetObjectMetadataAsync(
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

    /// <summary>
    /// Signed against the internal endpoint: this url is consumed by the Zeek service, which
    /// sits on the same network as the API, not by the browser.
    /// </summary>
    public Task<string> CreatePresignedDownloadUrlAsync(
        string objectKey,
        TimeSpan lifetime,
        CancellationToken cancellationToken = default)
    {
        return _internalClient.GetPreSignedURLAsync(new GetPreSignedUrlRequest
        {
            BucketName = _options.BucketName,
            Key = objectKey,
            Verb = HttpVerb.GET,
            Expires = DateTime.UtcNow.Add(lifetime),
            Protocol = ProtocolFor(_options.Endpoint)
        });
    }

    public async Task DeleteObjectAsync(
        string objectKey,
        CancellationToken cancellationToken = default)
    {
        await _internalClient.DeleteObjectAsync(
            new DeleteObjectRequest
            {
                BucketName = _options.BucketName,
                Key = objectKey
            },
            cancellationToken);
    }

    /// <summary>
    /// Browsers hand back the ETag exactly as MinIO sent it, quotes included; the SDK adds its
    /// own quoting, so strip them first.
    /// </summary>
    private static string NormalizeETag(string eTag) => eTag.Trim().Trim('"');

    /// <summary>Endpoint the browser reaches, falling back to the internal one.</summary>
    private string PublicEndpoint =>
        string.IsNullOrWhiteSpace(_options.PublicEndpoint)
            ? _options.Endpoint
            : _options.PublicEndpoint;

    /// <summary>
    /// Scheme to stamp into a presigned url.
    /// </summary>
    /// <remarks>
    /// This has to be set explicitly. AWSSDK.S3 v4 resolves the scheme for presigned urls
    /// through its endpoint provider rather than from <c>ServiceURL</c>, and defaults to
    /// https - so an endpoint configured as http still produced https:// links, and the
    /// browser failed with an SSL error against MinIO's plaintext port. Setting
    /// <c>AmazonS3Config.UseHttp</c> does not change it either; only the per-request
    /// Protocol does.
    ///
    /// The signature is unaffected: SigV4 signs the host header, not the scheme.
    /// </remarks>
    private static Protocol ProtocolFor(string endpoint) =>
        endpoint.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
            ? Protocol.HTTPS
            : Protocol.HTTP;
}
