using Application.DTOs.Diagnostics;

namespace Application.Interfaces;

/// <summary>
/// Byte-level storage operations used only to verify that the bucket works. These are kept off
/// <see cref="IFileStorageService"/> on purpose: the production upload path must never move
/// capture bytes through the API, and putting these methods on that interface would make doing
/// so look supported.
/// </summary>
public interface IStorageDiagnostics
{
    Task<UploadedObjectDto> UploadAsync(
        Stream content,
        string fileName,
        string contentType,
        CancellationToken cancellationToken = default);

    /// <summary>Returns the object's stream and content type, or null when it does not exist.</summary>
    Task<(Stream Content, string ContentType, long Length)?> DownloadAsync(
        string objectKey,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<StoredObjectDto>> ListAsync(
        string? prefix,
        int maxKeys,
        CancellationToken cancellationToken = default);

    /// <summary>Returns false when the object did not exist.</summary>
    Task<bool> DeleteAsync(
        string objectKey,
        CancellationToken cancellationToken = default);
}
