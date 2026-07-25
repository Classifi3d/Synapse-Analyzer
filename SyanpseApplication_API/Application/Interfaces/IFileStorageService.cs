using System;
using System.Collections.Generic;
using System.Text;

using Application.DTOs;

namespace Application.Interfaces;

public interface IFileStorageService
{
    Task<InitiateMultipartUploadResultDto> InitiateMultipartUploadAsync(
        string fileName,
        string contentType,
        int partCount);

    Task CompleteMultipartUploadAsync(
        string objectKey,
        string uploadId);

    Task<string> GenerateDownloadUrlAsync(string objectKey);

    Task<bool> ObjectExistsAsync(string objectKey);
}