using DistributedFileProcessor.Domain.Entities;

namespace DistributedFileProcessor.Domain.Interfaces;

public interface ITransactionRecordRepository
{
    Task<int> BulkInsertStreamAsync(IAsyncEnumerable<TransactionRecord> transactionsStream, CancellationToken cancellationToken = default);
    Task DeleteByJobIdAsync(Guid jobId, CancellationToken cancellationToken = default);
}