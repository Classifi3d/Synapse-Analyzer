namespace Application.Models;

/// <summary>An open S3 multipart upload plus one presigned PUT url per part.</summary>
public sealed record MultipartUploadSession(
    string UploadId,
    IReadOnlyList<PresignedPart> Parts,
    DateTime ExpiresAtUtc);

public sealed record PresignedPart(int PartNumber, string UploadUrl);

/// <summary>A part the client successfully uploaded, echoed back so S3 can assemble the object.</summary>
public sealed record CompletedPart(int PartNumber, string ETag);
