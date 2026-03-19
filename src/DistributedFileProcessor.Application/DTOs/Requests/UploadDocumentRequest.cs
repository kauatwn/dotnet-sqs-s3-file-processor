namespace DistributedFileProcessor.Application.DTOs.Requests;

public sealed record UploadDocumentRequest(string FileName, Stream ContentStream);