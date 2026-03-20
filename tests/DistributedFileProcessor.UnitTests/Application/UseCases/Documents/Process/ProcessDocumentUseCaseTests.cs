using DistributedFileProcessor.Application.Interfaces;
using DistributedFileProcessor.Application.UseCases.Documents.Process;
using DistributedFileProcessor.Domain.Entities;
using DistributedFileProcessor.Domain.Enums;
using DistributedFileProcessor.Domain.Interfaces;
using Microsoft.Extensions.Logging;
using Moq;

namespace DistributedFileProcessor.UnitTests.Application.UseCases.Documents.Process;

[Trait("Category", "Unit")]
public class ProcessDocumentUseCaseTests
{
    private readonly Mock<IFileStorageService> _fileStorageMock = new();
    private readonly Mock<IDocumentProcessJobRepository> _jobRepositoryMock = new();
    private readonly Mock<ITransactionFileParser> _fileParserMock = new();
    private readonly Mock<ITransactionRecordRepository> _transactionRepositoryMock = new();
    private readonly ILogger<ProcessDocumentUseCase> _logger = Mock.Of<ILogger<ProcessDocumentUseCase>>();

    private readonly ProcessDocumentUseCase _sut;

    public ProcessDocumentUseCaseTests()
    {
        _sut = new ProcessDocumentUseCase(_fileStorageMock.Object, _jobRepositoryMock.Object, _fileParserMock.Object, _transactionRepositoryMock.Object, _logger);
    }

    [Fact(DisplayName = "Should early return when job does not exist in database")]
    public async Task ExecuteAsync_ShouldReturnEarly_WhenJobIsNull()
    {
        // Arrange
        Guid invalidJobId = Guid.NewGuid();
        _jobRepositoryMock
            .Setup(x => x.GetByIdAsync(invalidJobId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((DocumentProcessJob?)null);

        // Act
        await _sut.ExecuteAsync(invalidJobId, TestContext.Current.CancellationToken);

        // Assert
        _fileStorageMock.Verify(x => x.DownloadFileAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _jobRepositoryMock.Verify(x => x.UpdateAsync(It.IsAny<DocumentProcessJob>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact(DisplayName = "Should change status to Failed and rethrow exception when parser fails")]
    public async Task ExecuteAsync_ShouldMarkAsFailedAndThrow_WhenExceptionOccurs()
    {
        // Arrange
        Guid jobId = Guid.NewGuid();
        DocumentProcessJob job = new("test.csv", "documents/test.csv");

        _jobRepositoryMock
            .Setup(x => x.GetByIdAsync(jobId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(job);

        _fileStorageMock
            .Setup(x => x.DownloadFileAsync(job.S3ObjectKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MemoryStream());

        _fileParserMock
            .Setup(x => x.ParseStreamAsync(It.IsAny<Stream>(), jobId, It.IsAny<CancellationToken>()))
            .Throws(new Exception("Simulated parser failure"));

        // Act
        Task Act() => _sut.ExecuteAsync(jobId, TestContext.Current.CancellationToken);

        // Assert
        await Assert.ThrowsAsync<Exception>(Act);
        Assert.Equal(ProcessStatus.Failed, job.Status);
        
        _jobRepositoryMock.Verify(x => x.UpdateAsync(job, It.IsAny<CancellationToken>()), Times.Exactly(2));
    }
    
    [Fact(DisplayName = "Should process in batches and insert remainders successfully")]
    public async Task ExecuteAsync_ShouldProcessInBatches_AndInsertRemainders()
    {
        // Arrange
        Guid jobId = Guid.NewGuid();
        DocumentProcessJob job = new("test.csv", "documents/test.csv");

        _jobRepositoryMock
            .Setup(x => x.GetByIdAsync(jobId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(job);
        
        using MemoryStream fakeStream = new();
        _fileStorageMock
            .Setup(x => x.DownloadFileAsync(job.S3ObjectKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(fakeStream);
        
        int totalRecordsToSimulate = 5002;
        _fileParserMock
            .Setup(x => x.ParseStreamAsync(It.IsAny<Stream>(), jobId, It.IsAny<CancellationToken>()))
            .Returns(CreateFakeTransactionsAsync(totalRecordsToSimulate, jobId));

        // Act
        await _sut.ExecuteAsync(jobId, TestContext.Current.CancellationToken);
        
        _transactionRepositoryMock.Verify(x => 
            x.BulkInsertAsync(It.IsAny<IEnumerable<TransactionRecord>>(), It.IsAny<CancellationToken>()), Times.Exactly(2));

        Assert.Equal(ProcessStatus.Completed, job.Status);
        _jobRepositoryMock.Verify(x => x.UpdateAsync(job, It.IsAny<CancellationToken>()), Times.Exactly(2));
    }
    
    private static async IAsyncEnumerable<TransactionRecord> CreateFakeTransactionsAsync(int count, Guid jobId)
    {
        IEnumerable<TransactionRecord> records = Enumerable.Range(0, count)
            .Select(_ => new TransactionRecord(jobId, DateTime.UtcNow, 100m, "Test", "ACC-123"));

        foreach (TransactionRecord record in records)
        {
            yield return record;
            await Task.Yield();
        }
    }
}