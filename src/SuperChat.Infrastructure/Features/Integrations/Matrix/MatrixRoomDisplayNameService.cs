using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using SuperChat.Infrastructure.Abstractions;
using SuperChat.Infrastructure.Persistence;

namespace SuperChat.Infrastructure.Services;

public sealed class MatrixRoomDisplayNameService(
    IDbContextFactory<SuperChatDbContext> dbContextFactory,
    MatrixApiClient matrixApiClient,
    IMemoryCache cache,
    ILogger<MatrixRoomDisplayNameService> logger) : IRoomDisplayNameService
{
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(15);

    public async Task<IReadOnlyDictionary<string, string>> ResolveManyAsync(
        Guid userId,
        IEnumerable<string> sourceRooms,
        CancellationToken cancellationToken)
    {
        var roomIds = sourceRooms
            .Where(LooksLikeMatrixRoomId)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (roomIds.Count == 0)
        {
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }

        var resolved = new Dictionary<string, string>(StringComparer.Ordinal);
        var unresolved = new List<string>();

        foreach (var roomId in roomIds)
        {
            if (cache.TryGetValue(GetCacheKey(userId, roomId), out string? cachedName) &&
                !string.IsNullOrWhiteSpace(cachedName))
            {
                resolved[roomId] = cachedName;
            }
            else
            {
                unresolved.Add(roomId);
            }
        }

        if (unresolved.Count == 0)
        {
            return resolved;
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var identity = await dbContext.MatrixIdentities
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.UserId == userId, cancellationToken);

        if (identity is null || string.IsNullOrWhiteSpace(identity.AccessToken))
        {
            return resolved;
        }

        var lookups = await Task.WhenAll(
            unresolved.Select(roomId => ResolveRoomNameAsync(
                identity.AccessToken,
                identity.MatrixUserId,
                roomId,
                cancellationToken)));
        foreach (var lookup in lookups)
        {
            if (string.IsNullOrWhiteSpace(lookup.DisplayName))
            {
                continue;
            }

            resolved[lookup.RoomId] = lookup.DisplayName;
            cache.Set(GetCacheKey(userId, lookup.RoomId), lookup.DisplayName, CacheDuration);
        }

        return resolved;
    }

    private async Task<(string RoomId, string? DisplayName)> ResolveRoomNameAsync(
        string accessToken,
        string? currentMatrixUserId,
        string roomId,
        CancellationToken cancellationToken)
    {
        try
        {
            var roomName = await matrixApiClient.GetRoomDisplayNameAsync(
                accessToken,
                roomId,
                currentMatrixUserId,
                cancellationToken);
            return (roomId, roomName);
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Failed to resolve display name for room {RoomId}.", roomId);
            return (roomId, null);
        }
    }

    private static string GetCacheKey(Guid userId, string roomId)
    {
        return $"matrix-room-name:{userId:N}:{roomId}";
    }

    private static bool LooksLikeMatrixRoomId(string value)
    {
        return !string.IsNullOrWhiteSpace(value) &&
               value.StartsWith("!", StringComparison.Ordinal) &&
               value.Contains(':', StringComparison.Ordinal);
    }
}
