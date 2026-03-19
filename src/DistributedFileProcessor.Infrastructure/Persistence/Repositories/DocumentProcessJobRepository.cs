using DistributedFileProcessor.Domain.Entities;
using DistributedFileProcessor.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DistributedFileProcessor.Infrastructure.Persistence.Repositories;

public sealed class DocumentProcessJobRepository(FileProcessorDbContext context) : IDocumentProcessJobRepository
{
    public async Task<DocumentProcessJob?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await context.DocumentProcessJobs
            .FirstOrDefaultAsync(j => j.Id == id, cancellationToken);
    }

    public async Task AddAsync(DocumentProcessJob job, CancellationToken cancellationToken = default)
    {
        await context.DocumentProcessJobs.AddAsync(job, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(DocumentProcessJob job, CancellationToken cancellationToken = default)
    {
        context.DocumentProcessJobs.Update(job);
        await context.SaveChangesAsync(cancellationToken);
    }
}