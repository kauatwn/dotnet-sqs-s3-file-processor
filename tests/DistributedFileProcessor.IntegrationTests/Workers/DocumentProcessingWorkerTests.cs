using Amazon.S3;
using Amazon.S3.Model;
using Amazon.SQS;
using Amazon.SQS.Model;
using DistributedFileProcessor.Domain.Entities;
using DistributedFileProcessor.Domain.Enums;
using DistributedFileProcessor.Infrastructure.Persistence;
using DistributedFileProcessor.IntegrationTests.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DistributedFileProcessor.IntegrationTests.Workers;

[Collection(nameof(IntegrationTestCollection))]
[Trait("Category", "Integration")]
public class DocumentProcessingWorkerTests(IntegrationTestWebAppFactory factory)
{
    private readonly IServiceScope _scope = factory.Services.CreateScope();

    [Fact(DisplayName = "Worker should process S3 Event natively, download file and insert records into DB")]
    public async Task Worker_ShouldProcessMessage_AndCompleteJobSuccessfully()
    {
        // Arrange
        var dbContext = _scope.ServiceProvider.GetRequiredService<FileProcessorDbContext>();
        var s3Client = _scope.ServiceProvider.GetRequiredService<IAmazonS3>();

        const string bucketName = "integration-test-bucket";
        var jobId = Guid.NewGuid();
        string s3Key = $"documents/{jobId}-test.csv";

        DocumentProcessJob job = new(jobId, "test.csv", s3Key);
        dbContext.DocumentProcessJobs.Add(job);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        const string csvContent = """
                                  Date,Amount,Description,AccountId
                                  2023-01-01,100.50,Test1,ACC-1
                                  2023-01-02,200.75,Test2,ACC-2
                                  """;

        // Act
        await s3Client.PutObjectAsync(new PutObjectRequest
        {
            BucketName = bucketName,
            Key = s3Key,
            ContentBody = csvContent
        }, TestContext.Current.CancellationToken);

        bool isProcessed = false;
        for (int i = 0; i < 20; i++)
        {
            _scope.ServiceProvider.GetRequiredService<FileProcessorDbContext>().ChangeTracker.Clear();
            DocumentProcessJob? currentJob = await dbContext.DocumentProcessJobs.FindAsync([jobId], TestContext.Current.CancellationToken);

            if (currentJob is { Status: ProcessStatus.Failed })
            {
                Assert.Fail($"O Worker falhou de forma inesperada: {currentJob.FailureReason}");
            }

            if (currentJob is { Status: ProcessStatus.Completed })
            {
                isProcessed = true;
                break;
            }

            await Task.Delay(1000, TestContext.Current.CancellationToken);
        }

        // Assert
        Assert.True(isProcessed, "O Worker demorou demais para processar ou a notificação do S3 falhou.");
        
        int insertedCount = dbContext.TransactionRecords.Count(x => x.JobId == jobId);
        Assert.Equal(2, insertedCount);
    }

    [Fact(DisplayName = "Worker should send message to DLQ when processing fails multiple times")]
    public async Task Worker_ShouldSendMessageToDlq_WhenProcessingFails()
    {
        // Arrange
        var dbContext = _scope.ServiceProvider.GetRequiredService<FileProcessorDbContext>();
        var s3Client = _scope.ServiceProvider.GetRequiredService<IAmazonS3>();
        var sqsClient = _scope.ServiceProvider.GetRequiredService<IAmazonSQS>();

        const string bucketName = "integration-test-bucket";
        var jobId = Guid.NewGuid();
        string s3Key = $"documents/{jobId}-invalid.csv";

        var config = factory.Services.GetRequiredService<IConfiguration>();
        string dlqName = config["AWS:SQS:DlqName"]!;

        GetQueueUrlResponse? dlqUrlResponse = await sqsClient.GetQueueUrlAsync(dlqName, TestContext.Current.CancellationToken);
        string dlqUrl = dlqUrlResponse.QueueUrl;

        DocumentProcessJob job = new(jobId, "invalid.csv", s3Key);
        dbContext.DocumentProcessJobs.Add(job);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        await s3Client.PutObjectAsync(new PutObjectRequest
        {
            BucketName = bucketName,
            Key = s3Key,
            ContentBody = """
                          Invalido,Invalido
                          1,2
                          """
        }, TestContext.Current.CancellationToken);

        bool jobMarkedAsFailed = false;
        for (int i = 0; i < 30; i++)
        {
            _scope.ServiceProvider.GetRequiredService<FileProcessorDbContext>().ChangeTracker.Clear();
            DocumentProcessJob? currentJob = await dbContext.DocumentProcessJobs.FindAsync([jobId], TestContext.Current.CancellationToken);

            if (currentJob is { Status: ProcessStatus.Failed })
            {
                jobMarkedAsFailed = true;
                break;
            }

            await Task.Delay(1000, TestContext.Current.CancellationToken);
        }

        // Assert 1: Garante que o domínio marcou o Job como Failed.
        Assert.True(jobMarkedAsFailed, "Job não foi marcado como Failed no banco de dados.");

        await Task.Delay(3000, TestContext.Current.CancellationToken);

        // Assert 2: Valida se a mensagem finalmente caiu na DLQ
        ReceiveMessageResponse? dlqResponse = await sqsClient.ReceiveMessageAsync(new ReceiveMessageRequest
        {
            QueueUrl = dlqUrl,
            MaxNumberOfMessages = 1,
            WaitTimeSeconds = 2
        }, TestContext.Current.CancellationToken);

        Assert.NotNull(dlqResponse);
        Assert.True(dlqResponse.Messages != null && dlqResponse.Messages.Count > 0, "A mensagem nunca chegou na DLQ!");
    }
}