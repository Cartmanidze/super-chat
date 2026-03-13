namespace SuperChat.Infrastructure.Abstractions;

public interface IQdrantClient
{
    Task EnsureMemoryCollectionAsync(CancellationToken cancellationToken);
}
