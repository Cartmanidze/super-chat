namespace SuperChat.Infrastructure.Abstractions;

public interface IRoomDisplayNameService
{
    Task<IReadOnlyDictionary<string, string>> ResolveManyAsync(
        Guid userId,
        IEnumerable<string> sourceRooms,
        CancellationToken cancellationToken);
}
