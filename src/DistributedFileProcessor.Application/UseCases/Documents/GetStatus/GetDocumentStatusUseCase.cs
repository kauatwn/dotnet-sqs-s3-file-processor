using DistributedFileProcessor.Application.DTOs.Responses;
using DistributedFileProcessor.Domain.Entities;
using DistributedFileProcessor.Domain.Interfaces;

namespace DistributedFileProcessor.Application.UseCases.Documents.GetStatus;

public sealed class GetDocumentStatusUseCase(IDocumentProcessJobRepository repository) : IGetDocumentStatusUseCase
{
    public async Task<DocumentStatusResponse?> ExecuteAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        DocumentProcessJob? job = await repository.GetByIdAsync(jobId, cancellationToken);

        if (job is null)
        {
            return null;
        }

        return new DocumentStatusResponse(job.Id, nameof(job.Status), job.S3ObjectKey);
    }
}