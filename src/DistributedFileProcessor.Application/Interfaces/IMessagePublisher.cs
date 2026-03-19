namespace DistributedFileProcessor.Application.Interfaces;

public interface IMessagePublisher
{
    Task PublishProcessJobAsync(Guid jobId, CancellationToken cancellationToken = default);
}