using DistributedFileProcessor.Application.Interfaces;
using DistributedFileProcessor.Application.UseCases.Documents.Process;

namespace DistributedFileProcessor.Worker;

public sealed partial class DocumentProcessingWorker(
    IServiceScopeFactory scopeFactory,
    IMessageConsumer messageConsumer,
    ILogger<DocumentProcessingWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        LogSqsConsumptionStarted(logger);
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await messageConsumer.ReceiveMessagesAsync(async (jobId, ct) =>
                {
                    using IServiceScope scope = scopeFactory.CreateScope();
                    var useCase = scope.ServiceProvider.GetRequiredService<IProcessDocumentUseCase>();

                    await useCase.ExecuteAsync(jobId, ct);
                }, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                LogSqsCommunicationFailed(logger, ex);
                await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
            }

            await Task.Delay(1000, cancellationToken);
        }
    }


    [LoggerMessage(LogLevel.Information, "SQS queue consumption started successfully.")]
    static partial void LogSqsConsumptionStarted(ILogger<DocumentProcessingWorker> logger);

    [LoggerMessage(LogLevel.Error,
        "A critical error occurred while communicating with SQS. Waiting before retrying...")]
    static partial void LogSqsCommunicationFailed(ILogger<DocumentProcessingWorker> logger, Exception ex);
}