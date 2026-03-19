using Amazon.S3;
using Amazon.S3.Model;
using DistributedFileProcessor.Application.Interfaces;
using DistributedFileProcessor.Infrastructure.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Registry;

namespace DistributedFileProcessor.Infrastructure.Storage;

public sealed partial class S3FileStorageService(
    IAmazonS3 s3Client,
    IOptions<S3Options> options,
    ResiliencePipelineProvider<string> pipelineProvider,
    ILogger<S3FileStorageService> logger) : IFileStorageService
{
    private readonly S3Options _options = options.Value;
    private readonly ResiliencePipeline _retryPipeline = pipelineProvider.GetPipeline("S3Pipeline");

    public async Task<string> UploadFileAsync(
        string fileName,
        Stream fileStream,
        CancellationToken cancellationToken = default)
    {
        string objectKey = $"documents/{Guid.NewGuid()}-{fileName}";

        PutObjectRequest request = new()
        {
            BucketName = _options.BucketName,
            Key = objectKey,
            InputStream = fileStream,
            AutoCloseStream = false
        };

        await _retryPipeline.ExecuteAsync(async ct => await s3Client.PutObjectAsync(request, ct), cancellationToken);
        LogFileUploaded(logger, objectKey, _options.BucketName);

        return objectKey;
    }

    public async Task<Stream> DownloadFileAsync(string s3ObjectKey, CancellationToken cancellationToken = default)
    {
        GetObjectRequest request = new()
        {
            BucketName = _options.BucketName,
            Key = s3ObjectKey
        };

        GetObjectResponse response = await _retryPipeline.ExecuteAsync(async ct =>
            await s3Client.GetObjectAsync(request, ct), cancellationToken);
        LogFileDownloaded(logger, s3ObjectKey, _options.BucketName);

        return response.ResponseStream;
    }

    [LoggerMessage(LogLevel.Information, "File {ObjectKey} successfully uploaded to bucket {BucketName}.")]
    static partial void LogFileUploaded(ILogger<S3FileStorageService> logger, string objectKey, string bucketName);

    [LoggerMessage(LogLevel.Information, "File {ObjectKey} successfully downloaded from bucket {BucketName}.")]
    static partial void LogFileDownloaded(ILogger<S3FileStorageService> logger, string objectKey, string bucketName);
}