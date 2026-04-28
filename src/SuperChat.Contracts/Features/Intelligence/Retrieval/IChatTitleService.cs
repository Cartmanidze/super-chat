namespace SuperChat.Contracts.Features.Intelligence.Retrieval;

public interface IChatTitleService
{
    Task<IReadOnlyDictionary<string, string>> ResolveManyAsync(
        Guid userId,
        IEnumerable<string> externalChatIds,
        CancellationToken cancellationToken);
}
