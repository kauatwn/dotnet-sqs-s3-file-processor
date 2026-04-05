namespace DistributedFileProcessor.Application.Interfaces;

public interface IFileStorageService
{
    Task<Stream> DownloadFileAsync(string s3ObjectKey, CancellationToken cancellationToken = default);
    Task<string> GeneratePreSignedUploadUrlAsync(string s3ObjectKey, TimeSpan expiration);
}