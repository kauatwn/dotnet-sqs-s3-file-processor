using DistributedFileProcessor.Application.DTOs.Responses;

namespace DistributedFileProcessor.Application.UseCases.Documents.GetStatus;

public interface IGetDocumentStatusUseCase
{
    Task<DocumentStatusResponse?> ExecuteAsync(Guid jobId, CancellationToken cancellationToken = default);
}