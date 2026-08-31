using DistributedFileProcessor.Domain.Entities;
using DistributedFileProcessor.Domain.Enums;

namespace DistributedFileProcessor.UnitTests.Domain.Entities;

[Trait("Category", "Unit")]
public class DocumentProcessJobTests
{
    [Fact(DisplayName = "Should create DocumentProcessJob with Pending status and generated Id")]
    public void Constructor_WithValidArguments_ShouldInitializeCorrectly()
    {
        // Arrange
        const string fileName = "test.csv";
        const string s3Key = "documents/test.csv";

        // Act
        DocumentProcessJob job = new(fileName, s3Key);

        // Assert
        Assert.NotEqual(Guid.Empty, job.Id);
        Assert.Equal(fileName, job.FileName);
        Assert.Equal(s3Key, job.S3ObjectKey);
        Assert.Equal(ProcessStatus.Pending, job.Status);
        Assert.Null(job.ProcessedAt);
        Assert.Null(job.FailureReason);
        Assert.True(job.CreatedAt <= DateTime.UtcNow);
    }

    [Fact(DisplayName = "Should create DocumentProcessJob with explicit Id")]
    public void Constructor_WithExplicitId_ShouldInitializeCorrectly()
    {
        // Arrange
        Guid expectedId = Guid.NewGuid();
        const string fileName = "test.csv";
        const string s3Key = "documents/test.csv";

        // Act
        DocumentProcessJob job = new(expectedId, fileName, s3Key);

        // Assert
        Assert.Equal(expectedId, job.Id);
        Assert.Equal(fileName, job.FileName);
        Assert.Equal(s3Key, job.S3ObjectKey);
        Assert.Equal(ProcessStatus.Pending, job.Status);
    }

    [Theory(DisplayName = "Should throw ArgumentException when constructor arguments are invalid")]
    [InlineData("", "documents/test.csv")]
    [InlineData("   ", "documents/test.csv")]
    [InlineData("test.csv", "")]
    [InlineData("test.csv", "   ")]
    public void Constructor_WithInvalidArguments_ShouldThrowArgumentException(string fileName, string s3Key)
    {
        // Act
        void Act() => _ = new DocumentProcessJob(fileName, s3Key);

        // Assert
        Assert.Throws<ArgumentException>(Act);
    }

    [Fact(DisplayName = "Should throw ArgumentOutOfRangeException when explicit Id is empty")]
    public void Constructor_WithEmptyGuid_ShouldThrowArgumentOutOfRangeException()
    {
        // Arrange
        Guid emptyId = Guid.Empty;
        const string fileName = "test.csv";
        const string s3Key = "documents/test.csv";

        // Act
        void Act() => _ = new DocumentProcessJob(emptyId, fileName, s3Key);

        // Assert
        Assert.Throws<ArgumentOutOfRangeException>(Act);
    }

    [Fact(DisplayName = "Should transition status from Pending to Processing")]
    public void MarkAsProcessing_WhenPending_ShouldTransitionToProcessing()
    {
        // Arrange
        DocumentProcessJob job = new("test.csv", "documents/test.csv");

        // Act
        job.MarkAsProcessing();

        // Assert
        Assert.Equal(ProcessStatus.Processing, job.Status);
        Assert.Null(job.FailureReason);
    }

    [Fact(DisplayName = "Should transition status from Failed to Processing on retry and clear FailureReason")]
    public void MarkAsProcessing_WhenFailed_ShouldTransitionToProcessingAndClearFailureReason()
    {
        // Arrange
        DocumentProcessJob job = new("test.csv", "documents/test.csv");
        job.MarkAsProcessing();
        job.MarkAsFailed("Transient network glitch");
        Assert.Equal(ProcessStatus.Failed, job.Status);
        Assert.Equal("Transient network glitch", job.FailureReason);

        // Act
        job.MarkAsProcessing();

        // Assert
        Assert.Equal(ProcessStatus.Processing, job.Status);
        Assert.Null(job.FailureReason);
    }

    [Fact(DisplayName = "Should throw InvalidOperationException when transitioning from Completed to Processing")]
    public void MarkAsProcessing_WhenCompleted_ShouldThrowInvalidOperationException()
    {
        // Arrange
        DocumentProcessJob job = new("test.csv", "documents/test.csv");
        job.MarkAsProcessing();
        job.MarkAsCompleted();

        // Act
        void Act() => job.MarkAsProcessing();

        // Assert
        var ex = Assert.Throws<InvalidOperationException>(Act);
        Assert.Contains("Completed", ex.Message);
    }

    [Fact(DisplayName = "Should transition status from Processing to Completed and set ProcessedAt")]
    public void MarkAsCompleted_WhenProcessing_ShouldTransitionToCompleted()
    {
        // Arrange
        DocumentProcessJob job = new("test.csv", "documents/test.csv");
        job.MarkAsProcessing();

        // Act
        job.MarkAsCompleted();

        // Assert
        Assert.Equal(ProcessStatus.Completed, job.Status);
        Assert.NotNull(job.ProcessedAt);
    }

    [Fact(DisplayName = "Should throw InvalidOperationException when marking as Completed without Processing")]
    public void MarkAsCompleted_WhenPending_ShouldThrowInvalidOperationException()
    {
        // Arrange
        DocumentProcessJob job = new("test.csv", "documents/test.csv");

        // Act
        void Act() => job.MarkAsCompleted();

        // Assert
        Assert.Throws<InvalidOperationException>(Act);
    }

    [Fact(DisplayName = "Should transition status to Failed and record FailureReason")]
    public void MarkAsFailed_ShouldSetStatusAndReason()
    {
        // Arrange
        DocumentProcessJob job = new("test.csv", "documents/test.csv");
        job.MarkAsProcessing();
        const string reason = "Database timeout error";

        // Act
        job.MarkAsFailed(reason);

        // Assert
        Assert.Equal(ProcessStatus.Failed, job.Status);
        Assert.Equal(reason, job.FailureReason);
        Assert.NotNull(job.ProcessedAt);
    }
}
