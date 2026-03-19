using DistributedFileProcessor.Application.DTOs.Requests;
using DistributedFileProcessor.Application.DTOs.Responses;

namespace DistributedFileProcessor.Application.UseCases.Documents.Upload;

public interface IUploadDocumentUseCase
{
    Task<UploadDocumentResponse> ExecuteAsync(UploadDocumentRequest request, CancellationToken cancellationToken = default);
}