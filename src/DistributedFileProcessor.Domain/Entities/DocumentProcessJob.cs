using DistributedFileProcessor.Domain.Enums;

namespace DistributedFileProcessor.Domain.Entities;

public sealed class DocumentProcessJob
{
    public Guid Id { get; private set; }
    public string FileName { get; private set; }
    public string S3ObjectKey { get; private set; }
    public ProcessStatus Status { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? ProcessedAt { get; private set; }
    public string? FailureReason { get; private set; }

    private DocumentProcessJob() { }

    public DocumentProcessJob(string fileName, string s3ObjectKey)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            throw new ArgumentException("File name must not be empty.", nameof(fileName));
        }

        if (string.IsNullOrWhiteSpace(s3ObjectKey))
        {
            throw new ArgumentException("S3 object key must not be empty.", nameof(s3ObjectKey));
        }

        Id = Guid.NewGuid();
        FileName = fileName;
        S3ObjectKey = s3ObjectKey;
        Status = ProcessStatus.Pending;
        CreatedAt = DateTime.UtcNow;
    }

    public void MarkAsProcessing()
    {
        if (Status != ProcessStatus.Pending)
        {
            throw new InvalidOperationException("Only documents with 'Pending' status can transition to 'Processing'.");
        }

        Status = ProcessStatus.Processing;
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