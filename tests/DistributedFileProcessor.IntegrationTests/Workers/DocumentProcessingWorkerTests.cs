using Amazon.S3;
using Amazon.S3.Model;
using Amazon.SQS;
using DistributedFileProcessor.Domain.Entities;
using DistributedFileProcessor.Domain.Enums;
using DistributedFileProcessor.Infrastructure.Persistence;
using DistributedFileProcessor.IntegrationTests.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;
using Amazon.SQS.Model;

namespace DistributedFileProcessor.IntegrationTests.Workers;

[Collection(nameof(IntegrationTestCollection))]
[Trait("Category", "Integration")]
public class DocumentProcessingWorkerTests(IntegrationTestWebAppFactory factory)
{
    private readonly IServiceScope _scope = factory.Services.CreateScope();

    [Fact(DisplayName = "Worker should process SQS message, download S3 file and insert records into DB")]
    public async Task Worker_ShouldProcessMessage_AndCompleteJobSuccessfully()
    {
        // Arrange
        var dbContext = _scope.ServiceProvider.GetRequiredService<FileProcessorDbContext>();
        var s3Client = _scope.ServiceProvider.GetRequiredService<IAmazonS3>();
        var sqsClient = _scope.ServiceProvider.GetRequiredService<IAmazonSQS>();

        const string bucketName = "integration-test-bucket";
        string s3Key = $"documents/{Guid.NewGuid()}-test.csv";
        string queueUrl = factory.Services.GetRequiredService<IConfiguration>()["AWS:SQS:QueueUrl"]!;

        DocumentProcessJob job = new("test.csv", s3Key);
        Guid jobId = job.Id;

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

        // Act
        string messageBody = JsonSerializer.Serialize(new { JobId = jobId });
        await sqsClient.SendMessageAsync(queueUrl, messageBody, TestContext.Current.CancellationToken);

        bool isProcessed = false;
        const int maxAttempts = 20;

        for (int i = 0; i < maxAttempts; i++)
        {
            dbContext.ChangeTracker.Clear();
            DocumentProcessJob? currentJob = await dbContext.DocumentProcessJobs.FindAsync([jobId], TestContext.Current.CancellationToken);

            if (currentJob is { Status: ProcessStatus.Failed })
            {
                Assert.Fail($"O Worker tentou processar, mas falhou. Motivo: {currentJob.FailureReason}");
            }

            if (currentJob is { Status: ProcessStatus.Completed })
            {
                isProcessed = true;
                break;
            }

            await Task.Delay(500, TestContext.Current.CancellationToken);
        }

        // Assert
        Assert.True(isProcessed, "O Worker demorou demais para processar ou não consumiu a mensagem.");

        int insertedRecordsCount = dbContext.TransactionRecords.Count(x => x.JobId == jobId);
        Assert.Equal(2, insertedRecordsCount);
    }
    
    [Fact(DisplayName = "Worker should send message to DLQ when processing fails multiple times")]
    public async Task Worker_ShouldSendMessageToDlq_WhenProcessingFails()
    {
        // Arrange
        var dbContext = _scope.ServiceProvider.GetRequiredService<FileProcessorDbContext>();
        var s3Client = _scope.ServiceProvider.GetRequiredService<IAmazonS3>();
        var sqsClient = _scope.ServiceProvider.GetRequiredService<IAmazonSQS>();

        const string bucketName = "integration-test-bucket";
        string s3Key = $"documents/{Guid.NewGuid()}-invalid.csv";
        
        var configuration = factory.Services.GetRequiredService<IConfiguration>();
        string queueUrl = configuration["AWS:SQS:QueueUrl"]!;
        string dlqName = configuration["AWS:SQS:DlqName"]!;
        
        GetQueueUrlResponse? dlqUrlResponse = await sqsClient.GetQueueUrlAsync(dlqName, TestContext.Current.CancellationToken);
        string dlqUrl = dlqUrlResponse.QueueUrl;

        DocumentProcessJob job = new("invalid.csv", s3Key);
        Guid jobId = job.Id;

        dbContext.DocumentProcessJobs.Add(job);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        const string invalidCsvContent = """
                                  WrongColumn1,WrongColumn2
                                  A,B
                                  """;

        await s3Client.PutObjectAsync(new PutObjectRequest
        {
            BucketName = bucketName,
            Key = s3Key,
            ContentBody = invalidCsvContent
        }, TestContext.Current.CancellationToken);

        // Act
        string messageBody = JsonSerializer.Serialize(new { JobId = jobId });
        await sqsClient.SendMessageAsync(queueUrl, messageBody, TestContext.Current.CancellationToken);

        bool jobMarkedAsFailed = false;
        DocumentProcessJob? currentJob = null;
        
        for (int i = 0; i < 30; i++)
        {
            dbContext.ChangeTracker.Clear();
            currentJob = await dbContext.DocumentProcessJobs.FindAsync([jobId], TestContext.Current.CancellationToken);

            if (currentJob is { Status: ProcessStatus.Failed })
            {
                jobMarkedAsFailed = true;
                break;
            }

            await Task.Delay(1000, TestContext.Current.CancellationToken);
        }

        // Assert 1: O caso de uso atualizou corretamente o banco após as falhas?
        Assert.True(jobMarkedAsFailed, "O Worker falhou, mas não marcou o Job como Failed no banco de dados no tempo limite.");
        Assert.NotNull(currentJob);
        Assert.False(string.IsNullOrWhiteSpace(currentJob.FailureReason), "O motivo da falha deveria ter sido preenchido.");

        // Assert 2: Como o Job falhou 3 vezes (e marcou no banco), o LocalStack DEVE ter movido a mensagem para a DLQ.
        // Vamos checar a DLQ agora (podemos tentar mais algumas vezes caso o LocalStack tenha um pequeno delay)
        bool messageInDlq = false;
        for (int i = 0; i < 5; i++)
        {
            ReceiveMessageResponse? receiveResponse = await sqsClient.ReceiveMessageAsync(new ReceiveMessageRequest
            {
                QueueUrl = dlqUrl,
                MaxNumberOfMessages = 1,
                WaitTimeSeconds = 1
            }, TestContext.Current.CancellationToken);

            if (receiveResponse?.Messages?.Count > 0)
            {
                messageInDlq = true;
                break;
            }
            await Task.Delay(1000, TestContext.Current.CancellationToken);
        }

        Assert.True(messageInDlq, "O Job falhou, mas a mensagem nunca foi movida para a DLQ pelo LocalStack.");
    }
}