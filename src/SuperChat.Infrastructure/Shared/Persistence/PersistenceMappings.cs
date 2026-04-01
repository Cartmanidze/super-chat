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
            TryParseAbsoluteUri(entity.WebLoginUrl),
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
            NormalizeConfidence(entity.Confidence));
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
            NormalizeConfidence(entity.Confidence),
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
            NormalizeConfidence(entity.Confidence),
            entity.ResolutionKind,
            entity.ResolutionSource,
            ToResolutionTrace(entity.ResolutionConfidence, entity.ResolutionModel, entity.ResolutionEvidenceJson),
            entity.ResolvedAt,
            entity.MeetingProvider,
            TryParseAbsoluteUri(entity.MeetingJoinUrl));
    }

    private static Uri? TryParseAbsoluteUri(string? value)
    {
        return !string.IsNullOrWhiteSpace(value) && Uri.TryCreate(value, UriKind.Absolute, out var uri)
            ? uri
            : null;
    }

    private static ResolutionTrace? ToResolutionTrace(
        double? confidence,
        string? model,
        string? evidenceJson)
    {
        var normalizedConfidence = NormalizeProbability(confidence);
        var normalizedModel = string.IsNullOrWhiteSpace(model) ? null : model;

        if (normalizedConfidence is null &&
            normalizedModel is null &&
            string.IsNullOrWhiteSpace(evidenceJson))
        {
            return null;
        }

        var evidence = TryDeserializeStringList(evidenceJson);
        if (normalizedConfidence is null &&
            normalizedModel is null &&
            evidence is null)
        {
            return null;
        }

        return new ResolutionTrace(normalizedConfidence, normalizedModel, evidence);
    }

    private static Confidence NormalizeConfidence(double value)
    {
        var normalizedValue = double.IsFinite(value)
            ? Math.Clamp(value, 0d, 1d)
            : 0d;

        return new Confidence(normalizedValue);
    }

    private static double? NormalizeProbability(double? value)
    {
        if (!value.HasValue)
        {
            return null;
        }

        return double.IsFinite(value.Value)
            ? Math.Clamp(value.Value, 0d, 1d)
            : 0d;
    }

    private static IReadOnlyList<string>? TryDeserializeStringList(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<List<string>>(json, JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
