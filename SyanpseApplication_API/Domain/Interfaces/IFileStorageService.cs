namespace Application.Interfaces;

public interface IFileStorageService
{
    Task<string> UploadFileAsync(Stream fileStream, string fileName, string contentType, long length);
    Task DownloadFileAsync(string storagePath, string destinationLocalPath);
}
