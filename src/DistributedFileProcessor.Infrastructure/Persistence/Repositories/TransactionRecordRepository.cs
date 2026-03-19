using DistributedFileProcessor.Domain.Entities;
using DistributedFileProcessor.Domain.Interfaces;
using EFCore.BulkExtensions;
using Microsoft.EntityFrameworkCore;

namespace DistributedFileProcessor.Infrastructure.Persistence.Repositories;

public sealed class TransactionRecordRepository(FileProcessorDbContext context) : ITransactionRecordRepository
{
    public async Task BulkInsertAsync(
        IEnumerable<TransactionRecord> transactions,
        CancellationToken cancellationToken = default)
    {
        await context.BulkInsertAsync(transactions, cancellationToken: cancellationToken);
    }

    public async Task DeleteByJobIdAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        await context.TransactionRecords
            .Where(t => t.JobId == jobId)
            .ExecuteDeleteAsync(cancellationToken);
    }
}