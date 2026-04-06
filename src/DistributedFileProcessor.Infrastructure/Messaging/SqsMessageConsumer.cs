using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Text.Json;
using Amazon.Lambda.S3Events;
using Amazon.SQS;
using Amazon.SQS.Model;
using DistributedFileProcessor.Application.Interfaces;
using DistributedFileProcessor.Infrastructure.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DistributedFileProcessor.Infrastructure.Messaging;

[ExcludeFromCodeCoverage(Justification = "Thin wrapper over AWS SDK. Behavior is validated through integration tests with LocalStack.")]
public sealed partial class SqsMessageConsumer(
    IAmazonSQS sqsClient,
    IOptions<SqsOptions> options,
    ILogger<SqsMessageConsumer> logger) : IMessageConsumer
{
    private static readonly JsonSerializerOptions JsonOptions = JsonSerializerOptions.Web;
    
    private readonly SqsOptions _options = options.Value;

    public async Task ReceiveMessagesAsync(Func<Guid, CancellationToken, Task> processMessageAction, CancellationToken cancellationToken = default)
    {
        ReceiveMessageRequest request = new()
        {
            QueueUrl = _options.QueueUrl,
            MaxNumberOfMessages = 1,
            WaitTimeSeconds = 5
        };

        ReceiveMessageResponse? response = await sqsClient.ReceiveMessageAsync(request, cancellationToken);

        if (response?.Messages is null || response.Messages.Count == 0)
        {
            return;
        }

        foreach (Message? message in response.Messages)
        {
            try
            {
                if (TryExtractJobIdFromS3Event(message.Body, logger, out Guid jobId))
                {
                    LogMessageProcessingStarted(logger, message.MessageId, jobId);
                    await processMessageAction(jobId, cancellationToken);

                    await sqsClient.DeleteMessageAsync(_options.QueueUrl, message.ReceiptHandle, cancellationToken);
                    LogMessageDeleted(logger, message.MessageId);
                }
                else
                {
                    LogMessageSkipped(logger, message.MessageId);
                    await sqsClient.DeleteMessageAsync(_options.QueueUrl, message.ReceiptHandle, cancellationToken);
                }
            }
            catch (Exception ex)
            {
                LogMessageProcessingFailed(logger, ex, message.MessageId);
            }
        }
    }

    private static bool TryExtractJobIdFromS3Event(string messageBody, ILogger logger, out Guid jobId)
    {
        jobId = Guid.Empty;
        try
        {
            var s3Event = JsonSerializer.Deserialize<S3Event>(messageBody, JsonOptions);
            S3Event.S3EventNotificationRecord? record = s3Event?.Records?.FirstOrDefault();

            if (record?.S3?.Object?.Key is not null)
            {
                string fullKey = WebUtility.UrlDecode(record.S3.Object.Key);
                string fileName = Path.GetFileName(fullKey);

                if (fileName.Length >= 36)
                {
                    return Guid.TryParse(fileName.AsSpan(0, 36), out jobId);
                }
            }
        }
        catch (Exception ex)
        {
            LogS3EventParseFailed(logger, ex);
        }

        return false;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Processing started for SQS message {MessageId} associated with Job {JobId}.")]
    static partial void LogMessageProcessingStarted(ILogger logger, string messageId, Guid jobId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Message {MessageId} successfully deleted from SQS (ACK).")]
    static partial void LogMessageDeleted(ILogger logger, string messageId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Message {MessageId} skipped. It does not contain a valid S3 Event or JobId.")]
    static partial void LogMessageSkipped(ILogger logger, string messageId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to parse S3 Event from message body. The payload might be invalid.")]
    static partial void LogS3EventParseFailed(ILogger logger, Exception ex);

    [LoggerMessage(Level = LogLevel.Error, Message = "Processing failed for SQS message {MessageId}. The SQS service will retry after the visibility timeout.")]
    static partial void LogMessageProcessingFailed(ILogger logger, Exception ex, string messageId);
}