using Application.Interfaces;
using Minio;
using Minio.DataModel.Args;

namespace Infrastructure.ExternalServices;

// Implementation of the interface using MinIO
public class MinioFileStorageService : IFileStorageService
{
    private readonly IMinioClient _minioClient;
    private readonly string _bucketName = "pcap-uploads"; // Can be moved to appsettings.json

    public MinioFileStorageService(IMinioClient minioClient)
    {
        _minioClient = minioClient;
    }

    public async Task<string> UploadFileAsync(Stream fileStream, string fileName, string contentType, long length)
    {
        // Ensure the bucket exists
        var bktExistArgs = new BucketExistsArgs().WithBucket(_bucketName);
        bool found = await _minioClient.BucketExistsAsync(bktExistArgs);

        if (!found)
        {
            var mkBktArgs = new MakeBucketArgs().WithBucket(_bucketName);
            await _minioClient.MakeBucketAsync(mkBktArgs);
        }

        // Create a unique object name to prevent overwrites
        var objectName = $"{Guid.NewGuid()}_{fileName}";

        // MinIO automatically handles large streams efficiently under the hood
        var putObjArgs = new PutObjectArgs()
            .WithBucket(_bucketName)
            .WithObject(objectName)
            .WithStreamData(fileStream)
            .WithObjectSize(length)
            .WithContentType(contentType);

        await _minioClient.PutObjectAsync(putObjArgs);

        return $"{_bucketName}/{objectName}";
    }
}

