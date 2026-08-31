using CsvHelper;
using CsvHelper.Configuration;
using DistributedFileProcessor.Application.Interfaces;
using DistributedFileProcessor.Domain.Entities;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;

namespace DistributedFileProcessor.Infrastructure.Parsing;

public sealed class CsvTransactionFileParser : ITransactionFileParser
{
    private const int BufferSize = 64 * 1024; // 64 KB buffer
    private sealed record CsvRow(DateTime Date, decimal Amount, string Description, string AccountId);

    public async IAsyncEnumerable<TransactionRecord> ParseStreamAsync(
        Stream fileStream,
        Guid jobId,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        using StreamReader reader = new(fileStream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, bufferSize: BufferSize, leaveOpen: true);

        CsvConfiguration config = new(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = true,
            Delimiter = ","
        };

        using CsvReader csv = new(reader, config);

        await foreach (CsvRow row in csv.GetRecordsAsync<CsvRow>(cancellationToken))
        {
            DateTime utcDate = DateTime.SpecifyKind(row.Date, DateTimeKind.Utc);
            yield return new TransactionRecord(jobId, utcDate, row.Amount, row.Description, row.AccountId);
        }
    }
}