namespace DistributedFileProcessor.API.Contracts.Responses;

public sealed record DocumentUploadUrlResponse(string Message, Guid JobId, string S3ObjectKey, string PreSignedUrl, string Status);