using System.Text.Json;
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

    [Fact(DisplayName = "Worker should process S3 Event via SQS, download file and insert records into DB")]
    public async Task Worker_ShouldProcessMessage_AndCompleteJobSuccessfully()
    {
        // Arrange
        var dbContext = _scope.ServiceProvider.GetRequiredService<FileProcessorDbContext>();
        var s3Client = _scope.ServiceProvider.GetRequiredService<IAmazonS3>();
        var sqsClient = _scope.ServiceProvider.GetRequiredService<IAmazonSQS>();

        const string bucketName = "integration-test-bucket";
        var jobId = Guid.NewGuid();

        string s3Key = $"documents/{jobId}-test.csv";
        string queueUrl = factory.Services.GetRequiredService<IConfiguration>()["AWS:SQS:QueueUrl"]!;

        DocumentProcessJob job = new(jobId, "test.csv", s3Key);
        dbContext.DocumentProcessJobs.Add(job);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        const string csvContent = """
                                  Date,Amount,Description,AccountId
                                  2023-01-01,100.50,Test1,ACC-1
                                  2023-01-02,200.75,Test2,ACC-2
                                  """;

        await s3Client.PutObjectAsync(new PutObjectRequest
        {
            BucketName = bucketName,
            Key = s3Key,
            ContentBody = csvContent
        }, TestContext.Current.CancellationToken);

        string messageBody = CreateS3EventNotificationJson(bucketName, s3Key);

        // Act
        await sqsClient.SendMessageAsync(queueUrl, messageBody, TestContext.Current.CancellationToken);

        bool isProcessed = false;
        for (int i = 0; i < 20; i++)
        {
            _scope.ServiceProvider.GetRequiredService<FileProcessorDbContext>().ChangeTracker.Clear();
            DocumentProcessJob? currentJob = await dbContext.DocumentProcessJobs.FindAsync([jobId], TestContext.Current.CancellationToken);

            if (currentJob is { Status: ProcessStatus.Failed })
            {
                Assert.Fail($"O Worker falhou: {currentJob.FailureReason}");
            }

            if (currentJob is { Status: ProcessStatus.Completed })
            {
                isProcessed = true;
                break;
            }

            await Task.Delay(1000, TestContext.Current.CancellationToken);
        }

        // Assert
        Assert.True(isProcessed, "O Worker demorou demais para processar ou ignorou a mensagem.");
        
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
        string queueUrl = config["AWS:SQS:QueueUrl"]!;
        string dlqName = config["AWS:SQS:DlqName"]!;

        GetQueueUrlResponse? dlqUrlResponse = await sqsClient.GetQueueUrlAsync(dlqName, TestContext.Current.CancellationToken);
        string dlqUrl = dlqUrlResponse.QueueUrl;

        DocumentProcessJob job = new(jobId, "invalid.csv", s3Key);
        dbContext.DocumentProcessJobs.Add(job);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        await s3Client.PutObjectAsync(new PutObjectRequest
        {
            BucketName = bucketName,
            Key = s3Key,
            ContentBody = """
                          Invalido,Invalido
                          1,2
                          """
        }, TestContext.Current.CancellationToken);

        // Act
        string messageBody = CreateS3EventNotificationJson(bucketName, s3Key);
        await sqsClient.SendMessageAsync(queueUrl, messageBody, TestContext.Current.CancellationToken);

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

        // Assert
        Assert.True(jobMarkedAsFailed, "Job não foi marcado como Failed.");

        ReceiveMessageResponse? dlqResponse = await sqsClient.ReceiveMessageAsync(new ReceiveMessageRequest
        {
            QueueUrl = dlqUrl,
            MaxNumberOfMessages = 1,
            WaitTimeSeconds = 1
        }, TestContext.Current.CancellationToken);

        Assert.NotEmpty(dlqResponse.Messages);
    }

    private static string CreateS3EventNotificationJson(string bucketName, string s3Key)
    {
        var s3Event = new
        {
            Records = new[]
            {
                new
                {
                    s3 = new
                    {
                        bucket = new { name = bucketName },
                        @object = new { key = s3Key }
                    }
                }
            }
        };

        return JsonSerializer.Serialize(s3Event);
    }
}