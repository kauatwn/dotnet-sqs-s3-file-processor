namespace DistributedFileProcessor.Application.DTOs.Responses;

public sealed record UploadDocumentResponse(Guid JobId, string S3ObjectKey, string Status);