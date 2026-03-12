using Microsoft.EntityFrameworkCore;
using SuperChat.Contracts.Configuration;
using SuperChat.Domain.Model;
using SuperChat.Infrastructure.Abstractions;
using SuperChat.Infrastructure.Persistence;

namespace SuperChat.Infrastructure.Services;

public sealed class AuthFlowService(
    IDbContextFactory<SuperChatDbContext> dbContextFactory,
    IMatrixProvisioningService matrixProvisioningService,
    PilotOptions pilotOptions,
    TimeProvider timeProvider) : IAuthFlowService
{
    public async Task<AppUser?> FindUserAsync(string email, CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var normalizedEmail = NormalizeEmail(email);

        var entity = await dbContext.AppUsers
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Email == normalizedEmail, cancellationToken);

        return entity?.ToDomain();
    }

    public async Task<MagicLinkRequestResult> RequestMagicLinkAsync(string email, CancellationToken cancellationToken)
    {
        var normalizedEmail = NormalizeEmail(email);

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var isAllowed = await dbContext.PilotInvites
            .AsNoTracking()
            .AnyAsync(item => item.Email == normalizedEmail && item.IsActive, cancellationToken);

        if (!isAllowed)
        {
            return new MagicLinkRequestResult(false, MagicLinkRequestStatus.NotInvited, "This email is not invited to the pilot yet.", null);
        }

        var now = timeProvider.GetUtcNow();
        var token = new MagicLinkTokenEntity
        {
            Value = Guid.NewGuid().ToString("N"),
            Email = normalizedEmail,
            CreatedAt = now,
            ExpiresAt = now.AddMinutes(pilotOptions.MagicLinkMinutes),
            Consumed = false
        };

        dbContext.MagicLinks.Add(token);
        await dbContext.SaveChangesAsync(cancellationToken);

        var link = new Uri($"{pilotOptions.BaseUrl.TrimEnd('/')}/auth/verify?token={Uri.EscapeDataString(token.Value)}");
        return new MagicLinkRequestResult(true, MagicLinkRequestStatus.Created, "Magic link created.", link);
    }

    public async Task<AuthVerificationResult> VerifyAsync(string token, CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var now = timeProvider.GetUtcNow();

        var link = await dbContext.MagicLinks
            .SingleOrDefaultAsync(item => item.Value == token && !item.Consumed, cancellationToken);

        if (link is null || link.ExpiresAt <= now)
        {
            return new AuthVerificationResult(false, AuthVerificationStatus.InvalidOrExpired, "This magic link is invalid or expired.", null);
        }

        var user = await CreateOrRefreshUserAsync(dbContext, link.Email, now, cancellationToken);
        link.Consumed = true;
        link.ConsumedByUserId = user.Id;

        await dbContext.SaveChangesAsync(cancellationToken);
        await matrixProvisioningService.EnsureIdentityAsync(user, cancellationToken);

        return new AuthVerificationResult(true, AuthVerificationStatus.Success, "Signed in successfully.", user);
    }

    private static async Task<AppUser> CreateOrRefreshUserAsync(
        SuperChatDbContext dbContext,
        string normalizedEmail,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var entity = await dbContext.AppUsers.SingleOrDefaultAsync(item => item.Email == normalizedEmail, cancellationToken);
        if (entity is null)
        {
            entity = new AppUserEntity
            {
                Id = Guid.NewGuid(),
                Email = normalizedEmail,
                CreatedAt = now,
                LastSeenAt = now
            };

            dbContext.AppUsers.Add(entity);
            return entity.ToDomain();
        }

        entity.LastSeenAt = now;
        return entity.ToDomain();
    }

    private static string NormalizeEmail(string email)
    {
        return email.Trim().ToLowerInvariant();
    }
}
