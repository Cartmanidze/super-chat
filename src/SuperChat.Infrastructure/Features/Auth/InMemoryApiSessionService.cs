using SuperChat.Contracts.Configuration;
using SuperChat.Domain.Model;
using SuperChat.Infrastructure.Abstractions;
using SuperChat.Infrastructure.State;

namespace SuperChat.Infrastructure.Services;

public sealed class InMemoryApiSessionService(
    SuperChatStore store,
    PilotOptions pilotOptions,
    TimeProvider timeProvider) : IApiSessionService
{
    public Task<ApiSession> IssueAsync(AppUser user, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var session = store.CreateApiSession(user, now.AddDays(pilotOptions.ApiSessionDays), now);
        return Task.FromResult(session);
    }

    public Task<AppUser?> GetUserAsync(string token, CancellationToken cancellationToken)
    {
        var user = store.FindUserBySessionToken(token, timeProvider.GetUtcNow());
        return Task.FromResult(user);
    }

    public Task RevokeAsync(string token, CancellationToken cancellationToken)
    {
        store.RevokeApiSession(token);
        return Task.CompletedTask;
    }
}
