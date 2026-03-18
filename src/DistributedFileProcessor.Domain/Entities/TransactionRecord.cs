namespace DistributedFileProcessor.Domain.Entities;

public sealed class TransactionRecord
{
    public Guid Id { get; private set; }
    public Guid JobId { get; private set; }
    public DateTime TransactionDate { get; private set; }
    public decimal Amount { get; private set; }
    public string Description { get; private set; }
    public string AccountId { get; private set; }

    private TransactionRecord() { }

    public TransactionRecord(Guid jobId, DateTime transactionDate, decimal amount, string description, string accountId)
    {
        if (jobId == Guid.Empty)
        {
            throw new ArgumentException("Job ID must not be empty.", nameof(jobId));
        }

        if (string.IsNullOrWhiteSpace(description))
        {
            throw new ArgumentException("Description is required.", nameof(description));
        }

        if (string.IsNullOrWhiteSpace(accountId))
        {
            throw new ArgumentException("Account ID is required.", nameof(accountId));
        }

        Id = Guid.NewGuid();
        JobId = jobId;
        TransactionDate = transactionDate;
        Amount = amount;
        Description = description;
        AccountId = accountId;
    }
}