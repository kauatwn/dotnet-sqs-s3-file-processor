namespace DistributedFileProcessor.Domain.Entities;

public sealed class TransactionRecord
{
    public Guid Id { get; private set; }
    public Guid JobId { get; private set; }
    public DateTime TransactionDate { get; private set; }
    public decimal Amount { get; private set; }
    public string Description { get; private set; } = null!;
    public string AccountId { get; private set; } = null!;

    // Required by Entity Framework Core for reflection-based materialization
    private TransactionRecord() { }

    public TransactionRecord(Guid jobId, DateTime transactionDate, decimal amount, string description, string accountId)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(jobId, Guid.Empty);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        ArgumentException.ThrowIfNullOrWhiteSpace(accountId);

        Id = Guid.NewGuid();
        JobId = jobId;
        TransactionDate = transactionDate;
        Amount = amount;
        Description = description;
        AccountId = accountId;
    }
}