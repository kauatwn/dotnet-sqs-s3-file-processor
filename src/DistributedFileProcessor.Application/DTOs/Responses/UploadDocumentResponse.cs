using DistributedFileProcessor.Domain.Enums;

namespace DistributedFileProcessor.Application.DTOs.Responses;

public sealed record UploadDocumentResponse(Guid JobId, string S3ObjectKey, string PreSignedUrl, ProcessStatus Status);