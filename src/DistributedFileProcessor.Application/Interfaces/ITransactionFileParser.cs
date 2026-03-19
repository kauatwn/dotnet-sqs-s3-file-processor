using DistributedFileProcessor.Domain.Entities;

namespace DistributedFileProcessor.Application.Interfaces;

public interface ITransactionFileParser
{
    IAsyncEnumerable<TransactionRecord> ParseStreamAsync(Stream fileStream, Guid jobId, CancellationToken cancellationToken = default);
}