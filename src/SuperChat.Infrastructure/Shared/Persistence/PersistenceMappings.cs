using System.Text.Json;
using SuperChat.Domain.Features.Auth;
using SuperChat.Domain.Features.Integrations.Matrix;
using SuperChat.Domain.Features.Integrations.Telegram;
using SuperChat.Domain.Features.Intelligence;
using SuperChat.Domain.Features.Messaging;

namespace SuperChat.Infrastructure.Shared.Persistence;

internal static class PersistenceMappings
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static AppUser ToDomain(this AppUserEntity entity)
    {
        return new AppUser(entity.Id, new Email(entity.Email), entity.CreatedAt, entity.LastSeenAt);
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
            new Confidence(entity.Confidence));
    }

    public static WorkItemRecord ToDomain(this WorkItemEntity entity)
    {
        return new WorkItemRecord(
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
            new Confidence(entity.Confidence),
            entity.ResolutionKind,
            entity.ResolutionSource,
            ToResolutionTrace(entity.ResolutionConfidence, entity.ResolutionModel, entity.ResolutionEvidenceJson),
            entity.ResolvedAt);
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
            new Confidence(entity.Confidence),
            entity.ResolutionKind,
            entity.ResolutionSource,
            ToResolutionTrace(entity.ResolutionConfidence, entity.ResolutionModel, entity.ResolutionEvidenceJson),
            entity.ResolvedAt,
            entity.MeetingProvider,
            string.IsNullOrWhiteSpace(entity.MeetingJoinUrl) ? null : new Uri(entity.MeetingJoinUrl, UriKind.Absolute));
    }

    private static ResolutionTrace? ToResolutionTrace(
        double? confidence,
        string? model,
        string? evidenceJson)
    {
        if (confidence is null &&
            string.IsNullOrWhiteSpace(model) &&
            string.IsNullOrWhiteSpace(evidenceJson))
        {
            return null;
        }

        IReadOnlyList<string>? evidence = null;
        if (!string.IsNullOrWhiteSpace(evidenceJson))
        {
            evidence = JsonSerializer.Deserialize<List<string>>(evidenceJson, JsonOptions);
        }

        return new ResolutionTrace(confidence, model, evidence);
    }
}
