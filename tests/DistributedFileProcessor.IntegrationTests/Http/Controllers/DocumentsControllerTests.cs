using DistributedFileProcessor.Application.DTOs.Responses;
using DistributedFileProcessor.IntegrationTests.Abstractions;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using DistributedFileProcessor.Domain.Entities;
using DistributedFileProcessor.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace DistributedFileProcessor.IntegrationTests.Http.Controllers;

[Collection(nameof(IntegrationTestCollection))]
[Trait("Category", "Integration")]
public class DocumentsControllerTests(IntegrationTestWebAppFactory factory)
{
    private const string BaseUrl = "/api/documents";

    private readonly HttpClient _client = factory.CreateClient();

    [Fact(DisplayName = $"POST {BaseUrl}/upload should return 202 Accepted and JobId")]
    public async Task UploadDocument_ShouldReturnAccepted_WhenFileIsValid()
    {
        // Arrange
        using MultipartFormDataContent multipartFormContent = new();

        const string csvContent = """
                            Date,Amount,Description,AccountId
                            2023-01-01,150.75,Supermarket,ACC-123
                            """;

        ByteArrayContent fileContent = new(Encoding.UTF8.GetBytes(csvContent));

        multipartFormContent.Add(fileContent, "file", "integration-test-upload.csv");

        // Act
        HttpResponseMessage response = await _client.PostAsync(
            $"{BaseUrl}/upload",
            multipartFormContent,
            TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);

        UploadDocumentResponse? result =
            await response.Content.ReadFromJsonAsync<UploadDocumentResponse>(TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.NotEqual(Guid.Empty, result.JobId);
        Assert.Contains("integration-test-upload.csv", result.S3ObjectKey);
    }
    
    [Fact(DisplayName = $"GET {BaseUrl}/{{jobId}} should return 200 OK when job exists")]
    public async Task GetDocumentStatus_ShouldReturnOk_WhenJobExists()
    {
        // Arrange
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FileProcessorDbContext>();
        
        DocumentProcessJob job = new("test-status.csv", "documents/test-status.csv");
        dbContext.DocumentProcessJobs.Add(job);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        HttpResponseMessage response = await _client.GetAsync($"{BaseUrl}/{job.Id}", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        DocumentStatusResponse? result = await response.Content.ReadFromJsonAsync<DocumentStatusResponse>(TestContext.Current.CancellationToken);
        Assert.NotNull(result);
        Assert.Equal(job.Id, result.JobId);
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