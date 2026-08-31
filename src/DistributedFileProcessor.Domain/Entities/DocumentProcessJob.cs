using DistributedFileProcessor.Domain.Enums;

namespace DistributedFileProcessor.Domain.Entities;

public sealed class DocumentProcessJob
{
    public Guid Id { get; private set; }
    public string FileName { get; private set; } = null!;
    public string S3ObjectKey { get; private set; } = null!;
    public ProcessStatus Status { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? ProcessedAt { get; private set; }
    public string? FailureReason { get; private set; }

    // Required by Entity Framework Core for reflection-based materialization
    private DocumentProcessJob() { }

    public DocumentProcessJob(string fileName, string s3ObjectKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentException.ThrowIfNullOrWhiteSpace(s3ObjectKey);

        Id = Guid.NewGuid();
        FileName = fileName;
        S3ObjectKey = s3ObjectKey;
        Status = ProcessStatus.Pending;
        CreatedAt = DateTime.UtcNow;
    }

    public DocumentProcessJob(Guid id, string fileName, string s3ObjectKey)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(id, Guid.Empty);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentException.ThrowIfNullOrWhiteSpace(s3ObjectKey);

        Id = id;
        FileName = fileName;
        S3ObjectKey = s3ObjectKey;
        Status = ProcessStatus.Pending;
        CreatedAt = DateTime.UtcNow;
    }

    public void MarkAsProcessing()
    {
        if (Status != ProcessStatus.Pending && Status != ProcessStatus.Failed)
        {
            throw new InvalidOperationException($"Only documents with 'Pending' or 'Failed' status can transition to 'Processing'. Current status: {Status}");
        }

        Status = ProcessStatus.Processing;
        FailureReason = null;
    }

    public void MarkAsCompleted()
    {
        if (Status != ProcessStatus.Processing)
        {
            throw new InvalidOperationException("Only documents with 'Processing' status can be marked as 'Completed'.");
        }

        Status = ProcessStatus.Completed;
        ProcessedAt = DateTime.UtcNow;
    }

    public void MarkAsFailed(string errorMessage)
    {
        Status = ProcessStatus.Failed;
        FailureReason = errorMessage;
        ProcessedAt = DateTime.UtcNow;
    }
}