namespace DistributedFileProcessor.API.Contracts.Responses;

public sealed record DocumentAcceptedResponse(string Message, Guid JobId, string S3ObjectKey, string Status);