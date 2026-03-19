using Amazon.S3;
using Amazon.S3.Util;
using Amazon.SQS;
using Amazon.SQS.Model;
using DistributedFileProcessor.Infrastructure.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace DistributedFileProcessor.Infrastructure.Extensions;

public static partial class LocalStackExtensions
{
    public static async Task EnsureLocalStackResourcesAsync(this IHost host)
    {
        using IServiceScope scope = host.Services.CreateScope();

        var s3Client = scope.ServiceProvider.GetRequiredService<IAmazonS3>();
        var sqsClient = scope.ServiceProvider.GetRequiredService<IAmazonSQS>();

        var s3Options = scope.ServiceProvider.GetRequiredService<IOptions<S3Options>>().Value;
        var sqsOptions = scope.ServiceProvider.GetRequiredService<IOptions<SqsOptions>>().Value;

        var logger = scope.ServiceProvider.GetRequiredService<ILogger<IAmazonSQS>>();

        await EnsureS3BucketAsync(s3Client, s3Options.BucketName, logger);
        await EnsureSqsQueuesAsync(sqsClient, sqsOptions, logger);
    }

    private static async Task EnsureS3BucketAsync(IAmazonS3 s3Client, string bucketName, ILogger logger)
    {
        try
        {
            bool bucketExists = await AmazonS3Util.DoesS3BucketExistV2Async(s3Client, bucketName);
            if (!bucketExists)
            {
                await s3Client.PutBucketAsync(bucketName);
                LogS3BucketCreated(logger, bucketName);
            }
        }
        catch (Exception ex)
        {
            LogS3BucketSetupFailed(logger, ex);
        }
    }

    private static async Task EnsureSqsQueuesAsync(IAmazonSQS sqsClient, SqsOptions options, ILogger logger)
    {
        try
        {
            string dlqArn = await CreateDeadLetterQueueAsync(sqsClient, options.DlqName);
            await CreateMainQueueAsync(sqsClient, options.QueueName, dlqArn);

            LogSqsQueuesLinked(logger, options.QueueName, options.DlqName);
        }
        catch (Exception ex)
        {
            LogSqsQueuesSetupFailed(logger, ex);
        }
    }

    private static async Task<string> CreateDeadLetterQueueAsync(IAmazonSQS sqsClient, string dlqName)
    {
        CreateQueueResponse createDlqResponse = await sqsClient.CreateQueueAsync(dlqName);

        // Busca o "CPF" (ARN) da fila recém-criada
        GetQueueAttributesResponse dlqAttributes = await sqsClient.GetQueueAttributesAsync(
            createDlqResponse.QueueUrl,
            ["QueueArn"]);

        return dlqAttributes.QueueARN;
    }

    private static async Task CreateMainQueueAsync(IAmazonSQS sqsClient, string mainQueueName, string dlqArn)
    {
        string redrivePolicyJson = JsonSerializer.Serialize(new
        {
            deadLetterTargetArn = dlqArn,
            maxReceiveCount = "3"
        });

        CreateQueueRequest createQueueRequest = new()
        {
            QueueName = mainQueueName,
            Attributes = new Dictionary<string, string>
            {
                { "RedrivePolicy", redrivePolicyJson }
            }
        };

        await sqsClient.CreateQueueAsync(createQueueRequest);
    }

    [LoggerMessage(LogLevel.Information, "S3 Bucket '{BucketName}' created successfully.")]
    static partial void LogS3BucketCreated(ILogger logger, string bucketName);

    [LoggerMessage(LogLevel.Warning, "A failure occurred while setting up the S3 Bucket.")]
    static partial void LogS3BucketSetupFailed(ILogger logger, Exception ex);

    [LoggerMessage(LogLevel.Information, "Main queue '{QueueName}' successfully linked to DLQ '{DlqName}'.")]
    static partial void LogSqsQueuesLinked(ILogger logger, string queueName, string dlqName);

    [LoggerMessage(LogLevel.Warning, "A failure occurred while setting up the SQS Queues.")]
    static partial void LogSqsQueuesSetupFailed(ILogger logger, Exception ex);
}