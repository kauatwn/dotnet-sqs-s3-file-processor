using Amazon.SQS;
using Amazon.SQS.Model;
using DistributedFileProcessor.Application.Interfaces;
using DistributedFileProcessor.Infrastructure.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Registry;
using System.Text.Json;

namespace DistributedFileProcessor.Infrastructure.Messaging;

public sealed partial class SqsMessagePublisher(
    IAmazonSQS sqsClient,
    IOptions<SqsOptions> options,
    ResiliencePipelineProvider<string> pipelineProvider,
    ILogger<SqsMessagePublisher> logger) : IMessagePublisher
{
    private readonly SqsOptions _options = options.Value;
    private readonly ResiliencePipeline _pipeline = pipelineProvider.GetPipeline("SqsPipeline");

    public async Task PublishProcessJobAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        string messageBody = JsonSerializer.Serialize(new { JobId = jobId });

        SendMessageRequest request = new()
        {
            QueueUrl = _options.QueueUrl,
            MessageBody = messageBody
        };

        await _pipeline.ExecuteAsync(async ct => await sqsClient.SendMessageAsync(request, ct), cancellationToken);
        LogMessagePublished(logger, jobId);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Message published to SQS successfully for Job {JobId}.")]
    static partial void LogMessagePublished(ILogger<SqsMessagePublisher> logger, Guid jobId);
}