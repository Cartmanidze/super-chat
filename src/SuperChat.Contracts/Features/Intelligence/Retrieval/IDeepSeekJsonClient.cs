namespace SuperChat.Contracts.Features.Intelligence.Retrieval;

public interface IDeepSeekJsonClient
{
    bool IsConfigured { get; }

    Task<TResponse?> CompleteJsonAsync<TResponse>(
        IReadOnlyList<DeepSeekMessage> messages,
        int maxTokens,
        CancellationToken cancellationToken) where TResponse : class;
}
