using DistributedFileProcessor.Domain.Entities;

namespace DistributedFileProcessor.Domain.Interfaces;

public interface ITransactionRecordRepository
{
    Task BulkInsertAsync(IEnumerable<TransactionRecord> transactions, CancellationToken cancellationToken = default);
    Task DeleteByJobIdAsync(Guid jobId, CancellationToken cancellationToken = default);
}