using SuperChat.Domain.Model;

namespace SuperChat.Domain.Abstractions;

public interface ISuperChatStore
{
    Task<bool> IsInviteAllowedAsync(string email, CancellationToken cancellationToken);
    Task<AppUser> GetOrCreateUserAsync(string email, CancellationToken cancellationToken);
    Task<AppUser?> GetUserAsync(Guid userId, CancellationToken cancellationToken);
    Task<IReadOnlyList<AppUser>> GetUsersAsync(CancellationToken cancellationToken);

    Task StoreMagicLinkAsync(MagicLinkToken magicLink, CancellationToken cancellationToken);
    Task<MagicLinkToken?> GetMagicLinkAsync(string token, CancellationToken cancellationToken);
    Task ConsumeMagicLinkAsync(string token, Guid userId, CancellationToken cancellationToken);

    Task<MatrixIdentity?> GetMatrixIdentityAsync(Guid userId, CancellationToken cancellationToken);
    Task UpsertMatrixIdentityAsync(MatrixIdentity identity, CancellationToken cancellationToken);

    Task<TelegramConnection?> GetTelegramConnectionAsync(Guid userId, CancellationToken cancellationToken);
    Task UpsertTelegramConnectionAsync(TelegramConnection connection, CancellationToken cancellationToken);

    Task<SyncCheckpoint?> GetSyncCheckpointAsync(Guid userId, CancellationToken cancellationToken);
    Task UpsertSyncCheckpointAsync(SyncCheckpoint checkpoint, CancellationToken cancellationToken);

    Task<bool> AddNormalizedMessageAsync(NormalizedMessage message, CancellationToken cancellationToken);
    Task<IReadOnlyList<NormalizedMessage>> GetRecentMessagesAsync(Guid userId, int take, CancellationToken cancellationToken);
    Task<IReadOnlyList<NormalizedMessage>> GetUnprocessedMessagesAsync(Guid userId, int take, CancellationToken cancellationToken);
    Task MarkMessagesProcessedAsync(Guid userId, IReadOnlyCollection<Guid> messageIds, CancellationToken cancellationToken);

    Task AddExtractedItemsAsync(Guid userId, IReadOnlyCollection<ExtractedItem> items, CancellationToken cancellationToken);
    Task<IReadOnlyList<ExtractedItem>> GetExtractedItemsAsync(Guid userId, CancellationToken cancellationToken);

    Task AddFeedbackAsync(FeedbackEvent feedback, CancellationToken cancellationToken);
}
