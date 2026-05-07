namespace DistributedFileProcessor.Application.Interfaces;

public interface IMessageConsumer
{
    Task ReceiveMessagesAsync(Func<Guid, CancellationToken, Task> processMessageAsync, CancellationToken cancellationToken = default);
}
