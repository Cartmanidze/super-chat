using SuperChat.Contracts.Features.Intelligence.Retrieval;

namespace SuperChat.Infrastructure.Features.Intelligence.Retrieval;

/// <summary>
/// Placeholder implementation until a real chat-name store is wired in.
/// Returns an empty dictionary; callers fall back to the raw chat id.
/// </summary>
internal sealed class EmptyRoomDisplayNameService : IRoomDisplayNameService
{
    public Task<IReadOnlyDictionary<string, string>> ResolveManyAsync(
        Guid userId,
        IEnumerable<string> sourceRooms,
        CancellationToken cancellationToken)
    {
        IReadOnlyDictionary<string, string> empty = new Dictionary<string, string>();
        return Task.FromResult(empty);
    }
}
