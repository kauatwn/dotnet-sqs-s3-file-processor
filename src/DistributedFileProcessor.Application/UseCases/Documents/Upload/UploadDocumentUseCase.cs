using DistributedFileProcessor.Application.DTOs.Requests;
using DistributedFileProcessor.Application.DTOs.Responses;
using DistributedFileProcessor.Application.Interfaces;
using DistributedFileProcessor.Domain.Entities;
using DistributedFileProcessor.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace DistributedFileProcessor.Application.UseCases.Documents.Upload;

public sealed partial class UploadDocumentUseCase(
    IFileStorageService fileStorage,
    IDocumentProcessJobRepository repository,
    IMessagePublisher messagePublisher,
    ILogger<UploadDocumentUseCase> logger) : IUploadDocumentUseCase
{
    public async Task<UploadDocumentResponse> ExecuteAsync(
        UploadDocumentRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            LogDocumentUploadStarted(logger, request.FileName);
            
            string s3ObjectKey = await fileStorage.UploadFileAsync(request.FileName, request.ContentStream, cancellationToken);
            DocumentProcessJob job = new(request.FileName, s3ObjectKey);
            
            await repository.AddAsync(job, cancellationToken);
            LogJobCreated(logger, job.Id, request.FileName);
            
            await messagePublisher.PublishProcessJobAsync(job.Id, cancellationToken);
            LogJobEnqueued(logger, job.Id);
            
            return new UploadDocumentResponse(job.Id, s3ObjectKey, nameof(job.Status));
        }
        catch (Exception ex)
        {
            LogDocumentUploadFailed(logger, ex, request.FileName);
            throw;
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Starting upload process for file {FileName}.")]
    static partial void LogDocumentUploadStarted(ILogger<UploadDocumentUseCase> logger, string fileName);

    [LoggerMessage(Level = LogLevel.Information, Message = "Document process job {JobId} created in database for file {FileName}.")]
    static partial void LogJobCreated(ILogger<UploadDocumentUseCase> logger, Guid jobId, string fileName);

    [LoggerMessage(Level = LogLevel.Information, Message = "Job {JobId} successfully enqueued for background processing.")]
    static partial void LogJobEnqueued(ILogger<UploadDocumentUseCase> logger, Guid jobId);

    [LoggerMessage(Level = LogLevel.Error, Message = "An error occurred while uploading and enqueueing document {FileName}.")]
    static partial void LogDocumentUploadFailed(ILogger<UploadDocumentUseCase> logger, Exception ex, string fileName);
}