using DistributedFileProcessor.Domain.Entities;

namespace DistributedFileProcessor.Domain.Interfaces;

public interface IDocumentProcessJobRepository
{
    Task<DocumentProcessJob?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task AddAsync(DocumentProcessJob job, CancellationToken cancellationToken = default);
    Task UpdateAsync(DocumentProcessJob job, CancellationToken cancellationToken = default);
}