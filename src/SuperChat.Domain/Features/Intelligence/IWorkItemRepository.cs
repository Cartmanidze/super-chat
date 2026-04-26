namespace SuperChat.Domain.Features.Intelligence;

public interface IWorkItemRepository
{
    Task AddRangeAsync(IReadOnlyList<WorkItemRecord> items, CancellationToken cancellationToken);
    Task<WorkItemRecord?> FindByIdAsync(Guid userId, Guid workItemId, CancellationToken cancellationToken);
    Task<IReadOnlyList<WorkItemRecord>> GetByUserAsync(Guid userId, bool unresolvedOnly, CancellationToken cancellationToken);
    Task<IReadOnlyList<WorkItemRecord>> SearchAsync(Guid userId, string query, int limit, CancellationToken cancellationToken);
    Task<IReadOnlyList<WorkItemRecord>> GetUnresolvedByRoomAsync(Guid userId, string externalChatId, CancellationToken cancellationToken);
    Task ResolveRelatedAsync(Guid userId, string sourceEventId, string resolutionKind, string resolutionSource, DateTimeOffset resolvedAt, CancellationToken cancellationToken);
    Task ResolveAsync(Guid workItemId, string resolutionKind, string resolutionSource, DateTimeOffset resolvedAt, CancellationToken cancellationToken);
    Task ResolveWithTraceAsync(Guid workItemId, string resolutionKind, string resolutionSource, DateTimeOffset resolvedAt, double? confidence, string? model, IReadOnlyList<string>? evidenceIds, CancellationToken cancellationToken);
}
