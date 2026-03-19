namespace DistributedFileProcessor.Infrastructure.Configuration;

public sealed class S3Options
{
    public const string SectionName = "AWS:S3";

    public string BucketName { get; init; } = string.Empty;
}