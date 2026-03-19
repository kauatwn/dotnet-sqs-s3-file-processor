namespace DistributedFileProcessor.Application.UseCases.Documents.Process;

public interface IProcessDocumentUseCase
{
    Task ExecuteAsync(Guid jobId, CancellationToken cancellationToken = default);
}