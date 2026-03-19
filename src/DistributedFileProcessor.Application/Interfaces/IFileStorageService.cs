namespace DistributedFileProcessor.Application.Interfaces;

public interface IFileStorageService
{
    Task<string> UploadFileAsync(string fileName, Stream fileStream, CancellationToken cancellationToken = default);
    Task<Stream> DownloadFileAsync(string s3ObjectKey, CancellationToken cancellationToken = default);
}