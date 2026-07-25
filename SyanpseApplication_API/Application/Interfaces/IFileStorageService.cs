using Application.Models;

namespace Application.Interfaces;

/// <summary>
/// Object storage boundary. Every method deals in coordinates and URLs - capture bytes
/// never pass through this interface, and therefore never through the API process.
/// </summary>
public interface IFileStorageService
{
    /// <summary>Bucket that captures are written to.</summary>
    string BucketName { get; }

    /// <summary>
    /// Chunk size the client must use when splitting the file. S3 requires every part
    /// except the last to be at least 5 MiB.
    /// </summary>
    long PartSizeBytes { get; }

    Task<MultipartUploadSession> InitiateMultipartUploadAsync(
        string objectKey,
        string contentType,
        int partCount,
        CancellationToken cancellationToken = default);

    /// <summary>Assembles the uploaded parts into a single object and returns its final size in bytes.</summary>
    Task<long> CompleteMultipartUploadAsync(
        string objectKey,
        string uploadId,
        IReadOnlyList<CompletedPart> parts,
        CancellationToken cancellationToken = default);

    Task AbortMultipartUploadAsync(
        string objectKey,
        string uploadId,
        CancellationToken cancellationToken = default);

    /// <summary>Returns the object size, or null when the object does not exist.</summary>
    Task<long?> GetObjectSizeAsync(
        string objectKey,
        CancellationToken cancellationToken = default);

    /// <summary>Time-limited GET url handed to the Zeek service so it can pull the capture itself.</summary>
    Task<string> CreatePresignedDownloadUrlAsync(
        string objectKey,
        TimeSpan lifetime,
        CancellationToken cancellationToken = default);

    Task DeleteObjectAsync(
        string objectKey,
        CancellationToken cancellationToken = default);
}
