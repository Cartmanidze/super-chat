using SuperChat.Contracts.Configuration;
using SuperChat.Domain.Model;
using SuperChat.Infrastructure.Abstractions;
using SuperChat.Infrastructure.State;

namespace SuperChat.Infrastructure.Services;

public sealed class InMemoryAuthFlowService(
    SuperChatStore store,
    IMatrixProvisioningService matrixProvisioningService,
    PilotOptions pilotOptions,
    TimeProvider timeProvider) : IAuthFlowService
{
    public AppUser? FindUser(string email)
    {
        return store.FindUserByEmail(email);
    }

    public Task<MagicLinkRequestResult> RequestMagicLinkAsync(string email, CancellationToken cancellationToken)
    {
        var normalizedEmail = email.Trim().ToLowerInvariant();
        if (!store.IsAllowedEmail(normalizedEmail))
        {
            return Task.FromResult(new MagicLinkRequestResult(false, "This email is not invited to the pilot yet.", null));
        }

        var expiresAt = timeProvider.GetUtcNow().AddMinutes(pilotOptions.MagicLinkMinutes);
        var token = store.CreateMagicLink(normalizedEmail, expiresAt);
        var link = new Uri($"{pilotOptions.BaseUrl.TrimEnd('/')}/auth/verify?token={Uri.EscapeDataString(token.Value)}");

        return Task.FromResult(new MagicLinkRequestResult(true, "Magic link created.", link));
    }

    public async Task<AuthVerificationResult> VerifyAsync(string token, CancellationToken cancellationToken)
    {
        var link = store.ConsumeMagicLink(token);
        if (link is null || link.ExpiresAt < timeProvider.GetUtcNow())
        {
            return new AuthVerificationResult(false, "This magic link is invalid or expired.", null);
        }

        var user = store.GetOrCreateUser(link.Email, timeProvider.GetUtcNow());
        await matrixProvisioningService.EnsureIdentityAsync(user, cancellationToken);

        return new AuthVerificationResult(true, "Signed in successfully.", user);
    }
}
