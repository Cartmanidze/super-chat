using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SuperChat.Contracts.Features.Operations;
using SuperChat.Domain.Features.Intelligence;
using SuperChat.Infrastructure.Features.Intelligence.Meetings;
using SuperChat.Infrastructure.Features.Intelligence.WorkItems;
using SuperChat.Infrastructure.Shared.Persistence;
using SuperChat.Infrastructure.Shared.Presentation;

namespace SuperChat.Infrastructure.Features.Intelligence.Resolution;

internal sealed class ConversationResolutionService(
    IDbContextFactory<SuperChatDbContext> dbContextFactory,
    IAiResolutionService aiResolutionService,
    WorkItemAutoResolutionService workItemAutoResolutionService,
    MeetingAutoResolutionService meetingAutoResolutionService,
    IOptions<ResolutionOptions> resolutionOptions)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task ResolveConversationAsync(
        Guid userId,
        string matrixRoomId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var candidates = await LoadConversationCandidatesAsync(userId, matrixRoomId, now, cancellationToken);
        var aiDecisions = await aiResolutionService.ResolveAsync(candidates, cancellationToken);
        if (aiDecisions.Count > 0)
        {
            await ApplyAiDecisionsAsync(userId, aiDecisions, cancellationToken);
        }

        await workItemAutoResolutionService.ResolveConversationAsync(userId, matrixRoomId, cancellationToken);
        await meetingAutoResolutionService.ResolveConversationAsync(userId, matrixRoomId, now, cancellationToken);
    }

    public async Task ResolveDueMeetingsAsync(
        Guid userId,
        string matrixRoomId,
        DateTimeOffset resolveAfter,
        CancellationToken cancellationToken)
    {
        var candidates = await LoadDueMeetingCandidatesAsync(userId, matrixRoomId, resolveAfter, cancellationToken);
        var aiDecisions = await aiResolutionService.ResolveAsync(candidates, cancellationToken);
        if (aiDecisions.Count > 0)
        {
            await ApplyAiDecisionsAsync(userId, aiDecisions, cancellationToken);
        }

        await meetingAutoResolutionService.ResolveDueMeetingsAsync(userId, matrixRoomId, resolveAfter, cancellationToken);
    }

    private async Task<IReadOnlyList<ConversationResolutionCandidate>> LoadConversationCandidatesAsync(
        Guid userId,
        string matrixRoomId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var options = resolutionOptions.Value;

        var workItems = await dbContext.WorkItems
            .AsNoTracking()
            .Where(item => item.UserId == userId &&
                           item.SourceRoom == matrixRoomId &&
                           item.ResolvedAt == null)
            .OrderBy(item => item.ObservedAt)
            .Take(Math.Max(1, options.MaxCandidatesPerRequest))
            .ToListAsync(cancellationToken);

        var meetings = await dbContext.Meetings
            .AsNoTracking()
            .Where(item => item.UserId == userId &&
                           item.SourceRoom == matrixRoomId &&
                           item.ResolvedAt == null)
            .OrderBy(item => item.ScheduledFor)
            .Take(Math.Max(1, options.MaxCandidatesPerRequest))
            .ToListAsync(cancellationToken);

        var observedFrom = new[]
            {
                workItems.Select(item => item.ObservedAt).DefaultIfEmpty(DateTimeOffset.MaxValue).Min(),
                meetings.Select(item => item.ObservedAt).DefaultIfEmpty(DateTimeOffset.MaxValue).Min()
            }
            .Min();

        if (observedFrom == DateTimeOffset.MaxValue)
        {
            return Array.Empty<ConversationResolutionCandidate>();
        }

        var messages = await dbContext.NormalizedMessages
            .AsNoTracking()
            .Where(item => item.UserId == userId &&
                           item.MatrixRoomId == matrixRoomId &&
                           item.SentAt >= observedFrom)
            .OrderBy(item => item.SentAt)
            .ThenBy(item => item.IngestedAt)
            .ToListAsync(cancellationToken);

        var result = new List<ConversationResolutionCandidate>(workItems.Count + meetings.Count);
        result.AddRange(workItems.Select(item => ToCandidate(item, messages, options.MaxMessagesPerCandidate)));
        result.AddRange(meetings.Select(item => ToCandidate(item, messages, options.MaxMessagesPerCandidate)));

        return SelectTopCandidates(result, now, options.MaxCandidatesPerRequest);
    }

    private async Task<IReadOnlyList<ConversationResolutionCandidate>> LoadDueMeetingCandidatesAsync(
        Guid userId,
        string matrixRoomId,
        DateTimeOffset resolveAfter,
        CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var options = resolutionOptions.Value;

        var meetings = await dbContext.Meetings
            .AsNoTracking()
            .Where(item => item.UserId == userId &&
                           item.SourceRoom == matrixRoomId &&
                           item.ResolvedAt == null &&
                           item.ScheduledFor <= resolveAfter)
            .OrderBy(item => item.ScheduledFor)
            .Take(Math.Max(1, options.MaxCandidatesPerRequest))
            .ToListAsync(cancellationToken);

        if (meetings.Count == 0)
        {
            return Array.Empty<ConversationResolutionCandidate>();
        }

        var observedFrom = meetings.Min(item => item.ObservedAt);
        var messages = await dbContext.NormalizedMessages
            .AsNoTracking()
            .Where(item => item.UserId == userId &&
                           item.MatrixRoomId == matrixRoomId &&
                           item.SentAt >= observedFrom)
            .OrderBy(item => item.SentAt)
            .ThenBy(item => item.IngestedAt)
            .ToListAsync(cancellationToken);

        var candidates = meetings
            .Select(item => ToCandidate(item, messages, options.MaxMessagesPerCandidate))
            .ToList();

        return SelectTopCandidates(candidates, resolveAfter, options.MaxCandidatesPerRequest);
    }

    private async Task ApplyAiDecisionsAsync(
        Guid userId,
        IReadOnlyList<AiResolutionDecisionResult> decisions,
        CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var decisionIds = decisions.Select(item => item.CandidateId).ToHashSet();

        var workItems = await dbContext.WorkItems
            .Where(item => item.UserId == userId && decisionIds.Contains(item.Id))
            .ToListAsync(cancellationToken);
        var meetings = await dbContext.Meetings
            .Where(item => item.UserId == userId && decisionIds.Contains(item.Id))
            .ToListAsync(cancellationToken);

        var changed = false;
        foreach (var decision in decisions)
        {
            var workItem = workItems.SingleOrDefault(item => item.Id == decision.CandidateId);
            if (workItem is not null)
            {
                changed |= ApplyResolution(workItem, decision);
            }

            var meeting = meetings.SingleOrDefault(item => item.Id == decision.CandidateId);
            if (meeting is not null)
            {
                changed |= ApplyResolution(meeting, decision);
            }
        }

        if (changed)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    private static bool ApplyResolution(WorkItemEntity entity, AiResolutionDecisionResult decision)
    {
        if (entity.IsResolved())
        {
            return false;
        }

        entity.ResolvedAt = decision.ResolvedAt;
        entity.ResolutionKind = decision.ResolutionKind;
        entity.ResolutionSource = decision.ResolutionSource;
        entity.ResolutionConfidence = decision.Confidence;
        entity.ResolutionModel = decision.Model;
        entity.ResolutionEvidenceJson = SerializeEvidence(decision.EvidenceMessageIds);
        entity.UpdatedAt = decision.ResolvedAt;
        return true;
    }

    private static bool ApplyResolution(MeetingEntity entity, AiResolutionDecisionResult decision)
    {
        if (entity.IsResolved())
        {
            return false;
        }

        entity.ResolvedAt = decision.ResolvedAt;
        entity.ResolutionKind = decision.ResolutionKind;
        entity.ResolutionSource = decision.ResolutionSource;
        entity.ResolutionConfidence = decision.Confidence;
        entity.ResolutionModel = decision.Model;
        entity.ResolutionEvidenceJson = SerializeEvidence(decision.EvidenceMessageIds);
        entity.UpdatedAt = decision.ResolvedAt;
        return true;
    }

    private static ConversationResolutionCandidate ToCandidate(
        WorkItemEntity item,
        IReadOnlyList<NormalizedMessageEntity> messages,
        int maxMessages)
    {
        return new ConversationResolutionCandidate(
            item.Id,
            ResolutionCandidateType.WorkItem,
            item.Kind,
            item.Title,
            item.Summary,
            item.SourceRoom,
            item.Person,
            item.ObservedAt,
            item.DueAt,
            LaterMessagesFor(item.ObservedAt, item.SourceEventId, messages, maxMessages));
    }

    private static ConversationResolutionCandidate ToCandidate(
        MeetingEntity item,
        IReadOnlyList<NormalizedMessageEntity> messages,
        int maxMessages)
    {
        return new ConversationResolutionCandidate(
            item.Id,
            ResolutionCandidateType.Meeting,
            ExtractedItemKind.Meeting,
            item.Title,
            item.Summary,
            item.SourceRoom,
            item.Person,
            item.ObservedAt,
            item.ScheduledFor,
            LaterMessagesFor(item.ObservedAt, item.SourceEventId, messages, maxMessages));
    }

    private static IReadOnlyList<ResolutionMessageSnippet> LaterMessagesFor(
        DateTimeOffset observedAt,
        string sourceEventId,
        IReadOnlyList<NormalizedMessageEntity> messages,
        int maxMessages)
    {
        return messages
            .Where(message => message.SentAt > observedAt ||
                              (message.SentAt == observedAt &&
                               !string.Equals(message.MatrixEventId, sourceEventId, StringComparison.Ordinal)))
            .Where(message => !string.IsNullOrWhiteSpace(message.Text))
            .Take(Math.Max(1, maxMessages))
            .Select(message => new ResolutionMessageSnippet(
                message.MatrixEventId,
                message.SenderName,
                message.Text.Trim(),
                message.SentAt))
            .ToList();
    }

    private static string? SerializeEvidence(IReadOnlyList<string>? evidenceMessageIds)
    {
        if (evidenceMessageIds is null || evidenceMessageIds.Count == 0)
        {
            return null;
        }

        return JsonSerializer.Serialize(evidenceMessageIds, JsonOptions);
    }

    private static IReadOnlyList<ConversationResolutionCandidate> SelectTopCandidates(
        IReadOnlyList<ConversationResolutionCandidate> candidates,
        DateTimeOffset now,
        int maxCandidates)
    {
        var selectedIds = ResolutionCandidateSelection
            .SelectTopCandidates(
                candidates.Select(item => new ResolutionCandidateInput(
                    item.Id,
                    item.Kind,
                    item.Title,
                    item.Summary,
                    item.Person,
                    item.ObservedAt,
                    item.DueAt,
                    item.LaterMessages.Select(message => new ResolutionEvidenceMessageInput(
                        message.SenderName,
                        message.Text,
                        message.SentAt)).ToList()))
                    .ToList(),
                now,
                maxCandidates)
            .Select(item => item.Id)
            .ToHashSet();

        return candidates
            .Where(item => selectedIds.Contains(item.Id))
            .OrderBy(item => item.ObservedAt)
            .ToList();
    }
}
