using SuperChat.Contracts.Configuration;
using SuperChat.Domain.Model;
using SuperChat.Infrastructure.Abstractions;
using SuperChat.Infrastructure.State;

namespace SuperChat.Infrastructure.Services;

public sealed class InMemoryMatrixProvisioningService(
    SuperChatStore store,
    MatrixOptions options,
    TimeProvider timeProvider) : IMatrixProvisioningService
{
    public Task<MatrixIdentity> EnsureIdentityAsync(AppUser user, CancellationToken cancellationToken)
    {
        var existing = store.GetMatrixIdentity(user.Id);
        if (existing is not null)
        {
            return Task.FromResult(existing);
        }

        var slug = user.Email.Split('@')[0].Replace(".", "-", StringComparison.OrdinalIgnoreCase);
        var server = new Uri(options.HomeserverUrl).Host;
        var identity = new MatrixIdentity(
            user.Id,
            $"@{options.UserIdPrefix}-{slug}:{server}",
            $"dev-token-{user.Id:N}",
            timeProvider.GetUtcNow());

        store.UpsertMatrixIdentity(identity);
        return Task.FromResult(identity);
    }
}
