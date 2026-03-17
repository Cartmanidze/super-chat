using SuperChat.Domain.Model;

namespace SuperChat.Infrastructure.Persistence;

internal static class PersistenceMappings
{
    public static AppUser ToDomain(this AppUserEntity entity)
    {
        return new AppUser(entity.Id, entity.Email, entity.CreatedAt, entity.LastSeenAt);
    }

    public static MagicLinkToken ToDomain(this MagicLinkTokenEntity entity)
    {
        return new MagicLinkToken(entity.Value, entity.Email, entity.CreatedAt, entity.ExpiresAt, entity.Consumed, entity.ConsumedByUserId);
    }

    public static ApiSession ToDomain(this ApiSessionEntity entity)
    {
        return new ApiSession(entity.UserId, entity.Token, entity.CreatedAt, entity.ExpiresAt);
    }

    public static MatrixIdentity ToDomain(this MatrixIdentityEntity entity)
    {
        return new MatrixIdentity(entity.UserId, entity.MatrixUserId, entity.AccessToken, entity.ProvisionedAt);
    }

    public static TelegramConnection ToDomain(this TelegramConnectionEntity entity)
    {
        return new TelegramConnection(
            entity.UserId,
            entity.State,
            string.IsNullOrWhiteSpace(entity.WebLoginUrl) ? null : new Uri(entity.WebLoginUrl, UriKind.Absolute),
            entity.UpdatedAt,
            entity.LastSyncedAt);
    }

    public static NormalizedMessage ToDomain(this NormalizedMessageEntity entity)
    {
        return new NormalizedMessage(
            entity.Id,
            entity.UserId,
            entity.Source,
            entity.MatrixRoomId,
            entity.MatrixEventId,
            entity.SenderName,
            entity.Text,
            entity.SentAt,
            entity.IngestedAt,
            entity.Processed);
    }

    public static ExtractedItem ToDomain(this ExtractedItemEntity entity)
    {
        return new ExtractedItem(
            entity.Id,
            entity.UserId,
            entity.Kind,
            entity.Title,
            entity.Summary,
            entity.SourceRoom,
            entity.SourceEventId,
            entity.Person,
            entity.ObservedAt,
            entity.DueAt,
            entity.Confidence);
    }

    public static MeetingRecord ToDomain(this MeetingEntity entity)
    {
        return new MeetingRecord(
            entity.Id,
            entity.UserId,
            entity.Title,
            entity.Summary,
            entity.SourceRoom,
            entity.SourceEventId,
            entity.Person,
            entity.ObservedAt,
            entity.ScheduledFor,
            entity.Confidence,
            entity.MeetingProvider,
            string.IsNullOrWhiteSpace(entity.MeetingJoinUrl) ? null : new Uri(entity.MeetingJoinUrl, UriKind.Absolute));
    }
}
