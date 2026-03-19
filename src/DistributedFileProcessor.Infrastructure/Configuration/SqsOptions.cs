namespace DistributedFileProcessor.Infrastructure.Configuration;

public sealed class SqsOptions
{
    public const string SectionName = "AWS:SQS";

    public string QueueUrl { get; init; } = string.Empty;
    public string QueueName { get; init; } = string.Empty;
    public string DlqName { get; init; } = string.Empty;
}