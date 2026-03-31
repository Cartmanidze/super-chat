namespace SuperChat.Contracts.Features.Intelligence.Retrieval;

public interface IRoomDisplayNameService
{
    Task<IReadOnlyDictionary<string, string>> ResolveManyAsync(
        Guid userId,
        IEnumerable<string> sourceRooms,
        CancellationToken cancellationToken);
}
