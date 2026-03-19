using Amazon.SQS;
using Amazon.SQS.Model;
using DistributedFileProcessor.Application.Interfaces;
using DistributedFileProcessor.Infrastructure.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace DistributedFileProcessor.Infrastructure.Messaging;

public sealed partial class SqsMessageConsumer(
    IAmazonSQS sqsClient,
    IOptions<SqsOptions> options,
    ILogger<SqsMessageConsumer> logger) : IMessageConsumer
{
    private readonly SqsOptions _options = options.Value;

    public async Task ReceiveMessagesAsync(
        Func<Guid, CancellationToken, Task> processMessageAction,
        CancellationToken cancellationToken = default)
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
                using JsonDocument document = JsonDocument.Parse(message.Body);
                Guid jobId = document.RootElement.GetProperty("JobId").GetGuid();

                LogMessageProcessingStarted(logger, message.MessageId, jobId);
                await processMessageAction(jobId, cancellationToken);
                await sqsClient.DeleteMessageAsync(
                    _options.QueueUrl,
                    message.ReceiptHandle,
                    cancellationToken);

                LogMessageDeleted(logger, message.MessageId);
            }
            catch (Exception ex)
            {
                LogMessageProcessingFailed(logger, ex, message.MessageId);

                try
                {
                    await sqsClient.ChangeMessageVisibilityAsync(
                        _options.QueueUrl,
                        message.ReceiptHandle,
                        0,
                        cancellationToken);
                }
                catch (Exception visibilityEx)
                {
                    logger.LogWarning(visibilityEx, "Failed to change message visibility for {MessageId}",
                        message.MessageId);
                }
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Processing started for SQS message {MessageId} associated with Job {JobId}.")]
    static partial void LogMessageProcessingStarted(ILogger<SqsMessageConsumer> logger, string messageId, Guid jobId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Message {MessageId} successfully deleted from SQS (ACK).")]
    static partial void LogMessageDeleted(ILogger<SqsMessageConsumer> logger, string messageId);

    [LoggerMessage(Level = LogLevel.Error,
        Message = "Processing failed for SQS message {MessageId}. Visibility timeout reset to 0 (NACK).")]
    static partial void LogMessageProcessingFailed(ILogger<SqsMessageConsumer> logger, Exception ex, string messageId);
}