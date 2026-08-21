using DistributedFileProcessor.Domain.Entities;
using DistributedFileProcessor.Domain.Interfaces;
using EFCore.BulkExtensions;
using Microsoft.EntityFrameworkCore;

namespace DistributedFileProcessor.Infrastructure.Persistence.Repositories;

public sealed class TransactionRecordRepository(FileProcessorDbContext context) : ITransactionRecordRepository
{
    public async Task<int> BulkInsertStreamAsync(IAsyncEnumerable<TransactionRecord> transactionsStream, CancellationToken cancellationToken = default)
    {
        const int batchSize = 5_000;
        List<TransactionRecord> currentBatch = new(batchSize);
        int totalProcessed = 0;

        BulkConfig bulkConfig = new()
        {
            BatchSize = batchSize,
            PreserveInsertOrder = false,
            SetOutputIdentity = false,
            EnableStreaming = true
        };

        await foreach (TransactionRecord transaction in transactionsStream.WithCancellation(cancellationToken))
        {
            currentBatch.Add(transaction);

            if (currentBatch.Count >= batchSize)
            {
                await context.BulkInsertAsync(currentBatch, bulkConfig, cancellationToken: cancellationToken);
                totalProcessed += currentBatch.Count;
                currentBatch.Clear();
            }
        }

        if (currentBatch.Count > 0)
        {
            await context.BulkInsertAsync(currentBatch, bulkConfig, cancellationToken: cancellationToken);
            totalProcessed += currentBatch.Count;
        }

        return totalProcessed;
    }

    public async Task DeleteByJobIdAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        await context.TransactionRecords
            .Where(t => t.JobId == jobId)
            .ExecuteDeleteAsync(cancellationToken);
    }
}