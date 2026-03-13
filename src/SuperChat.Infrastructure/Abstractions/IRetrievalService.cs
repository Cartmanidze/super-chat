namespace SuperChat.Infrastructure.Abstractions;

public interface IRetrievalService
{
    Task<IReadOnlyList<RetrievedChunk>> RetrieveAsync(RetrievalRequest request, CancellationToken cancellationToken);
}

public sealed record RetrievalRequest(
    Guid UserId,
    string QueryText,
    string QueryKind,
    string? ChatId = null,
    string? PeerId = null,
    string? Kind = null,
    int? Limit = null);

public sealed record RetrievedChunk(
    Guid ChunkId,
    string ChatId,
    string? PeerId,
    string Kind,
    string Text,
    DateTimeOffset TsFrom,
    DateTimeOffset TsTo,
    double Score);
