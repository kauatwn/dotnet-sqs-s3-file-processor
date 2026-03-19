namespace DistributedFileProcessor.Application.DTOs.Responses;

public sealed record DocumentStatusResponse(Guid JobId, string Status, string S3ObjectKey);