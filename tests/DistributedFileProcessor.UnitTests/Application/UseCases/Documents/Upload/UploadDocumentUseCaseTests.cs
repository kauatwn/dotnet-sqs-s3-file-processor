using DistributedFileProcessor.Application.DTOs.Requests;
using DistributedFileProcessor.Application.DTOs.Responses;
using DistributedFileProcessor.Application.Interfaces;
using DistributedFileProcessor.Application.UseCases.Documents.Upload;
using DistributedFileProcessor.Domain.Entities;
using DistributedFileProcessor.Domain.Interfaces;
using Microsoft.Extensions.Logging;
using Moq;
using System.Text;

namespace DistributedFileProcessor.UnitTests.Application.UseCases.Documents.Upload;

[Trait("Category", "Unit")]
public class UploadDocumentUseCaseTests
{
    private readonly Mock<IFileStorageService> _fileStorageMock = new();
    private readonly Mock<IDocumentProcessJobRepository> _repositoryMock = new();
    private readonly Mock<IMessagePublisher> _messagePublisherMock = new();
    private readonly ILogger<UploadDocumentUseCase> _logger = Mock.Of<ILogger<UploadDocumentUseCase>>();

    private readonly UploadDocumentUseCase _sut;

    public UploadDocumentUseCaseTests()
    {
        _sut = new UploadDocumentUseCase(_fileStorageMock.Object, _repositoryMock.Object, _messagePublisherMock.Object, _logger);
    }

    [Fact(DisplayName = "Should upload file, save job and publish message successfully")]
    public async Task ExecuteAsync_ShouldProcessSuccessfully()
    {
        // Arrange
        const string fileName = "test-transactions.csv";
        MemoryStream stream = new(Encoding.UTF8.GetBytes("fake csv content"));
        UploadDocumentRequest request = new(fileName, stream);

        string expectedS3Key = $"documents/{Guid.NewGuid()}-{fileName}";

        _fileStorageMock
            .Setup(x => x.UploadFileAsync(fileName, It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedS3Key);

        // Act
        UploadDocumentResponse response = await _sut.ExecuteAsync(request, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(response);
        Assert.NotEqual(Guid.Empty, response.JobId);
        Assert.Equal(expectedS3Key, response.S3ObjectKey);

        _fileStorageMock.Verify(x => x.UploadFileAsync(fileName, stream, It.IsAny<CancellationToken>()), Times.Once);

        _repositoryMock.Verify(x => x.AddAsync(
            It.Is<DocumentProcessJob>(j => j.FileName == fileName && j.S3ObjectKey == expectedS3Key),
            It.IsAny<CancellationToken>()), Times.Once);

        _messagePublisherMock.Verify(x => x.PublishProcessJobAsync(response.JobId, It.IsAny<CancellationToken>()), Times.Once);
    }
    
    [Fact(DisplayName = "Should log and rethrow exception when upload fails")]
    public async Task ExecuteAsync_ShouldLogAndThrow_WhenExceptionOccurs()
    {
        // Arrange
        const string fileName = "test-error.csv";
        UploadDocumentRequest request = new(fileName, Stream.Null); 

        _fileStorageMock
            .Setup(x => x.UploadFileAsync(fileName, It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Simulated AWS S3 failure"));

        // Act
        Task Act() => _sut.ExecuteAsync(request, TestContext.Current.CancellationToken);

        // Assert
        Exception exception = await Assert.ThrowsAsync<Exception>(Act);

        Assert.Equal("Simulated AWS S3 failure", exception.Message);

        _repositoryMock.Verify(x => 
            x.AddAsync(It.IsAny<DocumentProcessJob>(), It.IsAny<CancellationToken>()), Times.Never);
            
        _messagePublisherMock.Verify(x => 
            x.PublishProcessJobAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}