using DistributedFileProcessor.Application.Interfaces;
using DistributedFileProcessor.Domain.Entities;
using DistributedFileProcessor.Domain.Enums;
using DistributedFileProcessor.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace DistributedFileProcessor.Application.UseCases.Documents.Process;

public sealed partial class ProcessDocumentUseCase(
    IFileStorageService fileStorage,
    IDocumentProcessJobRepository jobRepository,
    ITransactionFileParser fileParser,
    ITransactionRecordRepository transactionRepository,
    ILogger<ProcessDocumentUseCase> logger) : IProcessDocumentUseCase
{
    public async Task ExecuteAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        DocumentProcessJob? job = await jobRepository.GetByIdAsync(jobId, cancellationToken);
        if (job is null)
        {
            LogJobNotFound(logger, jobId);
            return;
        }
        
        if (job.Status == ProcessStatus.Completed)
        {
            LogJobAlreadyProcessed(logger, jobId);
            return;
        }

        try
        {
            job.MarkAsProcessing();
            await jobRepository.UpdateAsync(job, cancellationToken);
            
            LogJobProcessingStarted(logger, jobId);
            
            await transactionRepository.DeleteByJobIdAsync(jobId, cancellationToken);
            
            await using Stream fileStream = await fileStorage.DownloadFileAsync(job.S3ObjectKey, cancellationToken);
            IAsyncEnumerable<TransactionRecord> transactionStream = fileParser.ParseStreamAsync(fileStream, jobId, cancellationToken);
            
            int totalProcessed = await transactionRepository.BulkInsertStreamAsync(transactionStream, cancellationToken);

            job.MarkAsCompleted();
            await jobRepository.UpdateAsync(job, cancellationToken);
            
            LogJobProcessed(logger, jobId, totalProcessed);
        }
        catch (Exception ex)
        {
            LogJobProcessingFailed(logger, ex, jobId);
            job.MarkAsFailed(ex.Message);
            await jobRepository.UpdateAsync(job, cancellationToken);

            throw;
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Job {JobId} not found in the database.")]
    static partial void LogJobNotFound(ILogger<ProcessDocumentUseCase> logger, Guid jobId);
    
    [LoggerMessage(Level = LogLevel.Information, Message = "Job {JobId} skipped because it is already Completed. Idempotency applied.")]
    static partial void LogJobAlreadyProcessed(ILogger<ProcessDocumentUseCase> logger, Guid jobId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Starting processing for Job {JobId}.")]
    static partial void LogJobProcessingStarted(ILogger<ProcessDocumentUseCase> logger, Guid jobId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Job {JobId} processed successfully! Total records inserted: {TotalProcessed}.")]
    static partial void LogJobProcessed(ILogger<ProcessDocumentUseCase> logger, Guid jobId, int totalProcessed);

    [LoggerMessage(Level = LogLevel.Error, Message = "A critical error occurred while processing Job {JobId}.")]
    static partial void LogJobProcessingFailed(ILogger<ProcessDocumentUseCase> logger, Exception ex, Guid jobId);
}