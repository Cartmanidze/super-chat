using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SuperChat.Contracts.Configuration;
using SuperChat.Domain.Model;
using SuperChat.Infrastructure.Abstractions;
using SuperChat.Infrastructure.Persistence;

namespace SuperChat.Infrastructure.Services;

public sealed class MatrixProvisioningService(
    IDbContextFactory<SuperChatDbContext> dbContextFactory,
    MatrixApiClient matrixApiClient,
    MatrixOptions options,
    TimeProvider timeProvider,
    ILogger<MatrixProvisioningService> logger) : IMatrixProvisioningService
{
    public async Task<MatrixIdentity> EnsureIdentityAsync(AppUser user, CancellationToken cancellationToken)
    {
        var existing = await GetIdentityAsync(user.Id, cancellationToken);
        if (existing is not null && !string.IsNullOrWhiteSpace(existing.AccessToken) && !IsBootstrapAccessToken(existing.AccessToken))
        {
            return existing;
        }

        if (string.IsNullOrWhiteSpace(options.AdminAccessToken))
        {
            return await EnsureBootstrapIdentityAsync(user, cancellationToken);
        }

        var matrixUserId = existing?.MatrixUserId ?? BuildMatrixUserId(user.Email);
        await matrixApiClient.EnsureUserAsync(matrixUserId, cancellationToken);
        var accessToken = await matrixApiClient.LoginAsUserAsync(matrixUserId, cancellationToken);

        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await db.MatrixIdentities.SingleOrDefaultAsync(item => item.UserId == user.Id, cancellationToken);
        if (entity is null)
        {
            entity = new MatrixIdentityEntity
            {
                UserId = user.Id,
                MatrixUserId = matrixUserId,
                AccessToken = accessToken,
                ProvisionedAt = timeProvider.GetUtcNow()
            };

            db.MatrixIdentities.Add(entity);
        }
        else
        {
            entity.MatrixUserId = matrixUserId;
            entity.AccessToken = accessToken;
            entity.ProvisionedAt = timeProvider.GetUtcNow();
        }

        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Provisioned Matrix identity {MatrixUserId} for user {UserId}.", matrixUserId, user.Id);

        return entity.ToDomain();
    }

    public async Task<MatrixIdentity?> GetIdentityAsync(Guid userId, CancellationToken cancellationToken)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var identity = await db.MatrixIdentities.AsNoTracking().SingleOrDefaultAsync(item => item.UserId == userId, cancellationToken);
        return identity?.ToDomain();
    }

    private async Task<MatrixIdentity> EnsureBootstrapIdentityAsync(AppUser user, CancellationToken cancellationToken)
    {
        var existing = await GetIdentityAsync(user.Id, cancellationToken);
        if (existing is not null)
        {
            return existing;
        }

        var entity = new MatrixIdentityEntity
        {
            UserId = user.Id,
            MatrixUserId = BuildMatrixUserId(user.Email),
            AccessToken = $"dev-token-{user.Id:N}",
            ProvisionedAt = timeProvider.GetUtcNow()
        };

        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        db.MatrixIdentities.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
        logger.LogWarning("Matrix admin token is missing. Falling back to bootstrap identity for user {UserId}.", user.Id);

        return entity.ToDomain();
    }

    private string BuildMatrixUserId(string email)
    {
        var server = new Uri(options.HomeserverUrl).Host;
        var slug = email.Split('@')[0]
            .Replace(".", "-", StringComparison.Ordinal)
            .Replace("_", "-", StringComparison.Ordinal);

        return $"@{options.UserIdPrefix}-{slug}:{server}";
    }

    private static bool IsBootstrapAccessToken(string accessToken)
    {
        return accessToken.StartsWith("dev-token-", StringComparison.Ordinal);
    }
}
