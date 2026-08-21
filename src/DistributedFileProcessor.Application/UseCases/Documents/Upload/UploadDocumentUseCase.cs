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
    ILogger<UploadDocumentUseCase> logger) : IUploadDocumentUseCase
{
    public async Task<UploadDocumentResponse> ExecuteAsync(
        UploadDocumentRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            LogUploadProcessStarted(logger, request.FileName);
            Guid jobId = Guid.NewGuid();
            string s3ObjectKey = $"documents/{jobId}-{request.FileName}";

            DocumentProcessJob job = new(jobId, request.FileName, s3ObjectKey);
            string preSignedUrl = await fileStorage.GeneratePreSignedUploadUrlAsync(s3ObjectKey, TimeSpan.FromMinutes(15));

            await repository.AddAsync(job, cancellationToken);
            LogJobPersisted(logger, job.Id, request.FileName);

            return new UploadDocumentResponse(job.Id, s3ObjectKey, preSignedUrl, job.Status);
        }
        catch (Exception ex)
        {
            LogUploadProcessFailed(logger, ex, request.FileName);
            throw;
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Initiating upload process for file: {FileName}.")]
    static partial void LogUploadProcessStarted(ILogger<UploadDocumentUseCase> logger, string fileName);

    [LoggerMessage(Level = LogLevel.Information, Message = "Job {JobId} successfully persisted for file {FileName}. Waiting for S3 event.")]
    static partial void LogJobPersisted(ILogger<UploadDocumentUseCase> logger, Guid jobId, string fileName);

    [LoggerMessage(Level = LogLevel.Error, Message = "Critical error during upload process for file {FileName}.")]
    static partial void LogUploadProcessFailed(ILogger<UploadDocumentUseCase> logger, Exception ex, string fileName);
}