using System.Net;
using System.Net.Http.Json;
using DistributedFileProcessor.API.Contracts.Responses;
using DistributedFileProcessor.Application.DTOs.Requests;
using DistributedFileProcessor.Application.DTOs.Responses;
using DistributedFileProcessor.Domain.Entities;
using DistributedFileProcessor.Domain.Enums;
using DistributedFileProcessor.Infrastructure.Persistence;
using DistributedFileProcessor.IntegrationTests.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace DistributedFileProcessor.IntegrationTests.Http.Controllers;

[Collection(nameof(IntegrationTestCollection))]
[Trait("Category", "Integration")]
public class DocumentsControllerTests(IntegrationTestWebAppFactory factory)
{
    private const string BaseUrl = "/api/documents";
    private readonly HttpClient _client = factory.CreateClient();

    [Fact(DisplayName = $"POST {BaseUrl}/upload-url should return 201 Created and Pre-signed URL")]
    public async Task RequestUploadUrl_ShouldReturnCreated_WhenRequestIsValid()
    {
        // Arrange
        UploadDocumentRequest request = new("integration-test.csv");

        // Act
        HttpResponseMessage response = await _client.PostAsJsonAsync($"{BaseUrl}/upload-url", request, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        
        DocumentUploadUrlResponse? result = await response.Content.ReadFromJsonAsync<DocumentUploadUrlResponse>(TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.NotEqual(Guid.Empty, result.JobId);
        Assert.NotEmpty(result.Url);
        Assert.Contains("integration-test.csv", result.S3ObjectKey);
        Assert.Equal(nameof(ProcessStatus.Pending), result.Status);

        string? locationHeader = response.Headers.Location?.ToString();
        Assert.NotNull(locationHeader);
        Assert.Contains($"{BaseUrl}/{result.JobId}", locationHeader, StringComparison.OrdinalIgnoreCase);
    }

    [Fact(DisplayName = $"POST {BaseUrl}/upload-url should return 400 Bad Request when FileName is empty")]
    public async Task RequestUploadUrl_ShouldReturnBadRequest_WhenFileNameIsEmpty()
    {
        // Arrange
        UploadDocumentRequest request = new(string.Empty);

        // Act
        HttpResponseMessage response = await _client.PostAsJsonAsync($"{BaseUrl}/upload-url", request, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact(DisplayName = $"GET {BaseUrl}/{{jobId}} should return 200 OK when job exists")]
    public async Task GetDocumentStatus_ShouldReturnOk_WhenJobExists()
    {
        // Arrange
        using IServiceScope scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FileProcessorDbContext>();

        var jobId = Guid.NewGuid();
        DocumentProcessJob job = new(jobId, "test-status.csv", "documents/test-status.csv");
        dbContext.DocumentProcessJobs.Add(job);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        HttpResponseMessage response = await _client.GetAsync($"{BaseUrl}/{jobId}", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        DocumentStatusResponse? result = await response.Content.ReadFromJsonAsync<DocumentStatusResponse>(TestContext.Current.CancellationToken);
        
        Assert.NotNull(result);
        Assert.Equal(jobId, result.JobId);
        Assert.Equal(job.Status.ToString(), result.Status);
    }

    [Fact(DisplayName = $"GET {BaseUrl}/{{jobId}} should return 404 Not Found when job does not exist")]
    public async Task GetDocumentStatus_ShouldReturnNotFound_WhenJobDoesNotExist()
    {
        // Act
        HttpResponseMessage response = await _client.GetAsync($"{BaseUrl}/{Guid.NewGuid()}", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}