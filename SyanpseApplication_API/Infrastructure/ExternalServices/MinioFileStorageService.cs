using Application.Interfaces;
using Infrastructure.Configuration;
using Microsoft.Extensions.Options;
using Minio;
using Minio.DataModel.Args;

namespace Infrastructure.ExternalServices;

public class MinioFileStorageService : IFileStorageService
{
    private readonly IMinioClient _minioClient;
    private readonly string _bucketName;

    public MinioFileStorageService(IMinioClient minioClient, IOptions<MinioOptions> options)
    {
        _minioClient = minioClient;
        _bucketName = options.Value.BucketName;
    }

    public async Task<string> UploadFileAsync(Stream fileStream, string fileName, string contentType, long length)
    {
        var bucketExistsRequest = new BucketExistsArgs().WithBucket(_bucketName);
        bool bucketFound = await _minioClient.BucketExistsAsync(bucketExistsRequest);

        if (!bucketFound)
        {
            var makeBucketRequest = new MakeBucketArgs().WithBucket(_bucketName);
            await _minioClient.MakeBucketAsync(makeBucketRequest);
        }

        var objectName = $"{Guid.NewGuid()}_{fileName}";

        var putObjectRequest = new PutObjectArgs()
            .WithBucket(_bucketName)
            .WithObject(objectName)
            .WithStreamData(fileStream)
            .WithObjectSize(length)
            .WithContentType(contentType);

        await _minioClient.PutObjectAsync(putObjectRequest);

        return $"{_bucketName}/{objectName}";
    }

    public async Task DownloadFileAsync(string storagePath, string destinationLocalPath)
    {
        // Parse the storage path format returned by UploadFileAsync (bucketName/objectName)
        var pathSegments = storagePath.Split('/', 2);
        if (pathSegments.Length != 2)
        {
            throw new ArgumentException("Invalid storage path format. Expected 'bucketName/objectName'.", nameof(storagePath));
        }

        var targetBucketName = pathSegments[0];
        var targetObjectName = pathSegments[1];

        // Ensure the directory for the local destination exists
        var destinationDirectory = Path.GetDirectoryName(destinationLocalPath);
        if (!string.IsNullOrWhiteSpace(destinationDirectory) && !Directory.Exists(destinationDirectory))
        {
            Directory.CreateDirectory(destinationDirectory);
        }

        var getObjectRequest = new GetObjectArgs()
            .WithBucket(targetBucketName)
            .WithObject(targetObjectName)
            .WithFile(destinationLocalPath);

        await _minioClient.GetObjectAsync(getObjectRequest);
    }
}