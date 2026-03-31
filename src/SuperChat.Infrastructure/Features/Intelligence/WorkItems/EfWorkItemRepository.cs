using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SuperChat.Domain.Features.Intelligence;
using SuperChat.Infrastructure.Shared.Persistence;

namespace SuperChat.Infrastructure.Features.Intelligence.WorkItems;

internal sealed class EfWorkItemRepository(
    IDbContextFactory<SuperChatDbContext> dbContextFactory)
    : EfCoreRepository<WorkItemEntity>(dbContextFactory), IWorkItemRepository
{
    public async Task AddRangeAsync(IReadOnlyList<WorkItemRecord> items, CancellationToken cancellationToken)
    {
        if (items.Count == 0) return;

        await using var db = await GetDbContextAsync(cancellationToken);
        var now = DateTimeOffset.UtcNow;

        foreach (var item in items)
        {
            db.WorkItems.Add(new WorkItemEntity
            {
                Id = item.Id,
                UserId = item.UserId,
                Kind = item.Kind,
                Title = item.Title,
                Summary = item.Summary,
                SourceRoom = item.SourceRoom,
                SourceEventId = item.SourceEventId,
                Person = item.Person,
                ObservedAt = item.ObservedAt,
                DueAt = item.DueAt,
                Confidence = item.Confidence.Value,
                ResolvedAt = item.ResolvedAt,
                ResolutionKind = item.ResolutionKind,
                ResolutionSource = item.ResolutionSource,
                ResolutionConfidence = item.ResolutionTrace?.Confidence,
                ResolutionModel = item.ResolutionTrace?.Model,
                ResolutionEvidenceJson = item.ResolutionTrace?.EvidenceMessageIds is { } ids
                    ? JsonSerializer.Serialize(ids)
                    : null,
                CreatedAt = now,
                UpdatedAt = now
            });
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<WorkItemRecord?> FindByIdAsync(Guid userId, Guid workItemId, CancellationToken cancellationToken)
    {
        await using var db = await GetDbContextAsync(cancellationToken);
        var entity = await db.WorkItems
            .AsNoTracking()
            .FirstOrDefaultAsync(w => w.Id == workItemId && w.UserId == userId, cancellationToken);
        return entity?.ToDomain();
    }

    public async Task<IReadOnlyList<WorkItemRecord>> GetByUserAsync(Guid userId, bool unresolvedOnly, CancellationToken cancellationToken)
    {
        await using var db = await GetDbContextAsync(cancellationToken);
        var query = db.WorkItems
            .AsNoTracking()
            .Where(w => w.UserId == userId);

        if (unresolvedOnly)
        {
            query = query.Where(w => w.ResolvedAt == null);
        }

        var entities = await query
            .OrderByDescending(w => w.ObservedAt)
            .ToListAsync(cancellationToken);

        return entities.Select(e => e.ToDomain()).ToList();
    }

    public async Task<IReadOnlyList<WorkItemRecord>> GetUnresolvedByRoomAsync(Guid userId, string matrixRoomId, CancellationToken cancellationToken)
    {
        await using var db = await GetDbContextAsync(cancellationToken);
        var entities = await db.WorkItems
            .AsNoTracking()
            .Where(w => w.UserId == userId && w.SourceRoom == matrixRoomId && w.ResolvedAt == null)
            .OrderByDescending(w => w.ObservedAt)
            .ToListAsync(cancellationToken);

        return entities.Select(e => e.ToDomain()).ToList();
    }

    public async Task ResolveAsync(Guid workItemId, string resolutionKind, string resolutionSource, DateTimeOffset resolvedAt, CancellationToken cancellationToken)
    {
        await using var db = await GetDbContextAsync(cancellationToken);
        var entity = await db.WorkItems.FirstOrDefaultAsync(w => w.Id == workItemId, cancellationToken);

        if (entity is null) return;

        entity.ResolvedAt = resolvedAt;
        entity.ResolutionKind = resolutionKind;
        entity.ResolutionSource = resolutionSource;
        entity.UpdatedAt = DateTimeOffset.UtcNow;

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task ResolveWithTraceAsync(Guid workItemId, string resolutionKind, string resolutionSource, DateTimeOffset resolvedAt, double? confidence, string? model, IReadOnlyList<string>? evidenceIds, CancellationToken cancellationToken)
    {
        await using var db = await GetDbContextAsync(cancellationToken);
        var entity = await db.WorkItems.FirstOrDefaultAsync(w => w.Id == workItemId, cancellationToken);

        if (entity is null) return;

        entity.ResolvedAt = resolvedAt;
        entity.ResolutionKind = resolutionKind;
        entity.ResolutionSource = resolutionSource;
        entity.ResolutionConfidence = confidence;
        entity.ResolutionModel = model;
        entity.ResolutionEvidenceJson = evidenceIds is { Count: > 0 }
            ? JsonSerializer.Serialize(evidenceIds)
            : null;
        entity.UpdatedAt = DateTimeOffset.UtcNow;

        await db.SaveChangesAsync(cancellationToken);
    }
}
