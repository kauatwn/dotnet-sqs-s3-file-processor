using System.Diagnostics.CodeAnalysis;
using Amazon.S3;
using Amazon.S3.Model;
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

[ExcludeFromCodeCoverage(Justification = "Infrastructure bootstrapping for LocalStack only. Exception scenarios are simulated environment failures.")]
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
        string mainQueueArn = await EnsureSqsQueuesAsync(sqsClient, sqsOptions, logger);
        
        await ConfigureS3ToSqsNotificationsAsync(s3Client, s3Options.BucketName, mainQueueArn, logger);
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

    private static async Task<string> EnsureSqsQueuesAsync(IAmazonSQS sqsClient, SqsOptions options, ILogger logger)
    {
        try
        {
            string dlqArn = await CreateDeadLetterQueueAsync(sqsClient, options.DlqName);
            string mainQueueArn = await CreateMainQueueAsync(sqsClient, options.QueueName, dlqArn);

            LogSqsQueuesLinked(logger, options.QueueName, options.DlqName);
            return mainQueueArn;
        }
        catch (Exception ex)
        {
            LogSqsQueuesSetupFailed(logger, ex);
            throw;
        }
    }

    private static async Task<string> CreateDeadLetterQueueAsync(IAmazonSQS sqsClient, string dlqName)
    {
        CreateQueueResponse createDlqResponse = await sqsClient.CreateQueueAsync(dlqName);
        GetQueueAttributesResponse dlqAttributes = await sqsClient.GetQueueAttributesAsync(createDlqResponse.QueueUrl, ["QueueArn"]);

        return dlqAttributes.QueueARN;
    }

    private static async Task<string> CreateMainQueueAsync(IAmazonSQS sqsClient, string mainQueueName, string dlqArn)
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
                { "RedrivePolicy", redrivePolicyJson },
                { "VisibilityTimeout", "60" }
            }
        };

        CreateQueueResponse createQueueResponse = await sqsClient.CreateQueueAsync(createQueueRequest);
        GetQueueAttributesResponse mainQueueAttributes = await sqsClient.GetQueueAttributesAsync(createQueueResponse.QueueUrl, ["QueueArn"]);

        return mainQueueAttributes.QueueARN;
    }

    private static async Task ConfigureS3ToSqsNotificationsAsync(IAmazonS3 s3Client, string bucketName, string queueArn, ILogger logger)
    {
        try
        {
            PutBucketNotificationRequest request = new()
            {
                BucketName = bucketName,
                QueueConfigurations =
                [
                    new QueueConfiguration
                    {
                        Events = [EventType.ObjectCreatedAll],
                        Queue = queueArn
                    }
                ]
            };

            await s3Client.PutBucketNotificationAsync(request);
            logger.LogInformation("S3 Event Notifications successfully configured to send events to SQS.");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "A failure occurred while setting up S3 Event Notifications.");
        }
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