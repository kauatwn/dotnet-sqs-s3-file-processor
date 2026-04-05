using DistributedFileProcessor.Application.DTOs.Requests;
using DistributedFileProcessor.Application.DTOs.Responses;
using DistributedFileProcessor.Application.Interfaces;
using DistributedFileProcessor.Application.UseCases.Documents.Upload;
using DistributedFileProcessor.Domain.Entities;
using DistributedFileProcessor.Domain.Interfaces;
using Microsoft.Extensions.Logging;
using Moq;

namespace DistributedFileProcessor.UnitTests.Application.UseCases.Documents.Upload;

[Trait("Category", "Unit")]
public class UploadDocumentUseCaseTests
{
    private readonly Mock<IFileStorageService> _fileStorageMock = new();
    private readonly Mock<IDocumentProcessJobRepository> _repositoryMock = new();

    private readonly ILogger<UploadDocumentUseCase> _logger = Mock.Of<ILogger<UploadDocumentUseCase>>();

    private readonly UploadDocumentUseCase _sut;

    public UploadDocumentUseCaseTests()
    {
        _sut = new UploadDocumentUseCase(_fileStorageMock.Object, _repositoryMock.Object, _logger);
    }

    [Fact(DisplayName = "Should generate pre-signed URL and save job successfully")]
    public async Task ExecuteAsync_ShouldProcessSuccessfully()
    {
        // Arrange
        const string fileName = "test-transactions.csv";
        const string expectedUrl = "https://s3.localstack.com/presigned-url";
        var request = new UploadDocumentRequest(fileName);

        _fileStorageMock
            .Setup(x => x.GeneratePreSignedUploadUrlAsync(It.IsAny<string>(), It.IsAny<TimeSpan>()))
            .ReturnsAsync(expectedUrl);

        // Act
        var response = await _sut.ExecuteAsync(request, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(response);
        Assert.NotEqual(Guid.Empty, response.JobId);
        Assert.Equal(expectedUrl, response.PreSignedUrl);

        Assert.Contains(response.JobId.ToString(), response.S3ObjectKey);
        Assert.Contains(fileName, response.S3ObjectKey);

        _fileStorageMock.Verify(x => x.GeneratePreSignedUploadUrlAsync(response.S3ObjectKey, It.IsAny<TimeSpan>()), Times.Once);

        _repositoryMock.Verify(x => x.AddAsync(
            It.Is<DocumentProcessJob>(j =>
                j.Id == response.JobId &&
                j.FileName == fileName &&
                j.S3ObjectKey == response.S3ObjectKey),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName = "Should log and rethrow exception when URL generation fails")]
    public async Task ExecuteAsync_ShouldLogAndThrow_WhenExceptionOccurs()
    {
        // Arrange
        UploadDocumentRequest request = new("error.csv");

        _fileStorageMock
            .Setup(x => x.GeneratePreSignedUploadUrlAsync(It.IsAny<string>(), It.IsAny<TimeSpan>()))
            .ThrowsAsync(new Exception("S3 failure"));

        // Act
        Task<UploadDocumentResponse> Act() => _sut.ExecuteAsync(request, TestContext.Current.CancellationToken);

        // Assert
        await Assert.ThrowsAsync<Exception>(Act);

        _repositoryMock.Verify(x => x.AddAsync(It.IsAny<DocumentProcessJob>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}