using SuperChat.Contracts;

namespace SuperChat.Infrastructure.Abstractions;

public interface IDeepSeekJsonClient
{
    bool IsConfigured { get; }

    Task<TResponse?> CompleteJsonAsync<TResponse>(
        IReadOnlyList<DeepSeekMessage> messages,
        int maxTokens,
        CancellationToken cancellationToken) where TResponse : class;
}
