using DistributedFileProcessor.Application.DTOs.Responses;
using DistributedFileProcessor.Application.UseCases.Documents.GetStatus;
using DistributedFileProcessor.Domain.Entities;
using DistributedFileProcessor.Domain.Enums;
using DistributedFileProcessor.Domain.Interfaces;
using Moq;

namespace DistributedFileProcessor.UnitTests.Application.UseCases.Documents.GetStatus;

[Trait("Category", "Unit")]
public class GetDocumentStatusUseCaseTests
{
    private readonly Mock<IDocumentProcessJobRepository> _repositoryMock = new();
    private readonly GetDocumentStatusUseCase _sut;

    public GetDocumentStatusUseCaseTests()
    {
        _sut = new GetDocumentStatusUseCase(_repositoryMock.Object);
    }

    [Fact(DisplayName = "Should return mapped status response when job exists")]
    public async Task ExecuteAsync_ShouldReturnStatus_WhenJobExists()
    {
        // Arrange
        DocumentProcessJob job = new("test.csv", "documents/test.csv");
        Guid generatedJobId = job.Id; 
        job.MarkAsProcessing();

        _repositoryMock
            .Setup(x => x.GetByIdAsync(generatedJobId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(job);

        // Act
        DocumentStatusResponse? response = await _sut.ExecuteAsync(generatedJobId, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(response);
        Assert.Equal(generatedJobId, response.JobId);
        Assert.Equal(nameof(ProcessStatus.Processing), response.Status);
        Assert.Equal(job.S3ObjectKey, response.S3ObjectKey); 
    }

    [Fact(DisplayName = "Should return null when job does not exist")]
    public async Task ExecuteAsync_ShouldReturnNull_WhenJobDoesNotExist()
    {
        // Arrange
        _repositoryMock
            .Setup(x => x.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((DocumentProcessJob?)null);

        // Act
        DocumentStatusResponse? response = await _sut.ExecuteAsync(Guid.NewGuid(), TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(response);
    }
}