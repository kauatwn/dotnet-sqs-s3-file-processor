using System.Diagnostics.CodeAnalysis;
using Amazon.S3;
using Amazon.S3.Model;
using DistributedFileProcessor.Application.Interfaces;
using DistributedFileProcessor.Infrastructure.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Registry;

namespace DistributedFileProcessor.Infrastructure.Storage;

[ExcludeFromCodeCoverage(Justification = "Thin wrapper over AWS SDK. Behavior is validated through integration tests with LocalStack.")]
public sealed partial class S3FileStorageService(
    IAmazonS3 s3Client,
    IOptions<S3Options> options,
    ResiliencePipelineProvider<string> pipelineProvider,
    ILogger<S3FileStorageService> logger) : IFileStorageService
{
    private readonly S3Options _options = options.Value;
    private readonly ResiliencePipeline _retryPipeline = pipelineProvider.GetPipeline("S3Pipeline");

    public async Task<Stream> DownloadFileAsync(string s3ObjectKey, CancellationToken cancellationToken = default)
    {
        GetObjectRequest request = new()
        {
            BucketName = _options.BucketName,
            Key = s3ObjectKey
        };

        GetObjectResponse response = await _retryPipeline.ExecuteAsync(async ct => await s3Client.GetObjectAsync(request, ct), cancellationToken);
        LogFileDownloaded(logger, s3ObjectKey, _options.BucketName);

        return response.ResponseStream;
    }

    public async Task<string> GeneratePreSignedUploadUrlAsync(string s3ObjectKey, TimeSpan expiration)
    {
        GetPreSignedUrlRequest request = new()
        {
            BucketName = _options.BucketName,
            Key = s3ObjectKey,
            Verb = HttpVerb.PUT,
            Expires = DateTime.UtcNow.Add(expiration)
        };

        string url = await s3Client.GetPreSignedURLAsync(request);
        LogPreSignedUrlGenerated(logger, s3ObjectKey, _options.BucketName, expiration.TotalMinutes);

        return url;
    }

    [LoggerMessage(LogLevel.Information, "File {ObjectKey} successfully downloaded from bucket {BucketName}.")]
    static partial void LogFileDownloaded(ILogger<S3FileStorageService> logger, string objectKey, string bucketName);

    [LoggerMessage(LogLevel.Information, "Pre-signed PUT URL generated safely for object {ObjectKey} in bucket {BucketName}. Expires in {ExpirationMinutes} minutes.")]
    static partial void LogPreSignedUrlGenerated(ILogger<S3FileStorageService> logger, string objectKey, string bucketName, double expirationMinutes);
}