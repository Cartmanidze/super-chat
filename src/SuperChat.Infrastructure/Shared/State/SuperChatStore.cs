using System.Collections.Concurrent;
using SuperChat.Contracts.Configuration;
using SuperChat.Domain.Model;

namespace SuperChat.Infrastructure.State;

public sealed class SuperChatStore
{
    private readonly ConcurrentDictionary<string, PilotInvite> _invites;
    private readonly ConcurrentDictionary<string, AppUser> _usersByEmail = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, MagicLinkToken> _magicLinks = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<Guid, MatrixIdentity> _matrixIdentities = new();
    private readonly ConcurrentDictionary<Guid, TelegramConnection> _connections = new();
    private readonly ConcurrentDictionary<string, NormalizedMessage> _messages = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<Guid, ExtractedItem> _items = new();
    private readonly ConcurrentDictionary<Guid, DateTimeOffset> _seededUsers = new();
    private readonly ConcurrentQueue<string> _feedback = new();

    public SuperChatStore(PilotOptions options)
    {
        _invites = new ConcurrentDictionary<string, PilotInvite>(
            options.AllowedEmails
            .Select(email => email.Trim().ToLowerInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(normalized =>
            {
                return new KeyValuePair<string, PilotInvite>(normalized, new PilotInvite(normalized, "bootstrap", DateTimeOffset.UtcNow, true));
            }),
            StringComparer.OrdinalIgnoreCase);
    }

    public int AllowedEmailCount => _invites.Count;
    public int KnownUserCount => _usersByEmail.Count;
    public int PendingMessageCount => _messages.Values.Count(message => !message.Processed);
    public int ExtractedItemCount => _items.Count;

    public MagicLinkToken CreateMagicLink(string email, DateTimeOffset expiresAt)
    {
        var token = new MagicLinkToken(Guid.NewGuid().ToString("N"), email, DateTimeOffset.UtcNow, expiresAt, false, null);
        _magicLinks[token.Value] = token;
        return token;
    }

    public MagicLinkToken? ConsumeMagicLink(string token)
    {
        if (!_magicLinks.TryGetValue(token, out var value) || value.Consumed)
        {
            return null;
        }

        _magicLinks[token] = value with { Consumed = true };
        return value;
    }

    public AppUser? FindUserByEmail(string email)
    {
        _usersByEmail.TryGetValue(email.Trim().ToLowerInvariant(), out var user);
        return user;
    }

    public AppUser GetOrCreateUser(string email, DateTimeOffset now)
    {
        var normalized = email.Trim().ToLowerInvariant();
        var existing = _usersByEmail.GetOrAdd(normalized, static (value, timestamp) => new AppUser(Guid.NewGuid(), value, timestamp, timestamp), now);
        var refreshed = existing with { LastSeenAt = now };
        _usersByEmail[normalized] = refreshed;
        return refreshed;
    }

    public IReadOnlyList<TelegramConnection> GetConnectionsReadyForDevelopmentSync()
    {
        return _connections.Values
            .Where(connection => connection.State == TelegramConnectionState.Connected && !_seededUsers.ContainsKey(connection.UserId))
            .ToList();
    }

    public TelegramConnection GetConnection(Guid userId)
    {
        return _connections.GetOrAdd(
            userId,
            static id => new TelegramConnection(id, TelegramConnectionState.NotStarted, null, DateTimeOffset.UtcNow, null));
    }

    public IReadOnlyList<ExtractedItem> GetExtractedItems(Guid userId)
    {
        return _items.Values
            .Where(item => item.UserId == userId)
            .OrderByDescending(item => item.ObservedAt)
            .ToList();
    }

    public MatrixIdentity? GetMatrixIdentity(Guid userId)
    {
        _matrixIdentities.TryGetValue(userId, out var identity);
        return identity;
    }

    public IReadOnlyList<NormalizedMessage> GetPendingMessages()
    {
        return _messages.Values.Where(message => !message.Processed).OrderBy(message => message.SentAt).ToList();
    }

    public IReadOnlyList<NormalizedMessage> GetRecentMessages(Guid userId, int take)
    {
        return _messages.Values
            .Where(message => message.UserId == userId)
            .OrderByDescending(message => message.SentAt)
            .Take(take)
            .ToList();
    }

    public bool IsAllowedEmail(string email)
    {
        return _invites.TryGetValue(email.Trim().ToLowerInvariant(), out var invite) && invite.IsActive;
    }

    public void MarkDemoSeeded(Guid userId, DateTimeOffset at)
    {
        _seededUsers[userId] = at;

        if (_connections.TryGetValue(userId, out var connection))
        {
            _connections[userId] = connection with { LastSyncedAt = at, UpdatedAt = at };
        }
    }

    public void MarkMessagesProcessed(IEnumerable<Guid> messageIds)
    {
        foreach (var messageId in messageIds)
        {
            var entry = _messages.Values.FirstOrDefault(message => message.Id == messageId);
            if (entry is null)
            {
                continue;
            }

            _messages[BuildMessageKey(entry.UserId, entry.MatrixRoomId, entry.MatrixEventId)] = entry with { Processed = true };
        }
    }

    public void AddExtractedItems(IEnumerable<ExtractedItem> items)
    {
        foreach (var item in items)
        {
            _items.TryAdd(item.Id, item);
        }
    }

    public void RecordFeedback(Guid userId, string area, bool useful, string? note)
    {
        _feedback.Enqueue($"{DateTimeOffset.UtcNow:o}|{userId}|{area}|{useful}|{note}");
    }

    public bool TryAddMessage(NormalizedMessage message)
    {
        return _messages.TryAdd(BuildMessageKey(message.UserId, message.MatrixRoomId, message.MatrixEventId), message);
    }

    public void UpsertConnection(TelegramConnection connection)
    {
        _connections[connection.UserId] = connection;
    }

    public void UpsertMatrixIdentity(MatrixIdentity identity)
    {
        _matrixIdentities[identity.UserId] = identity;
    }

    private static string BuildMessageKey(Guid userId, string roomId, string eventId)
    {
        return $"{userId:N}:{roomId}:{eventId}";
    }
}
