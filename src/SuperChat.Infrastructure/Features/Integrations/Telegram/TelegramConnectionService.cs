using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SuperChat.Contracts.Features.Auth;
using SuperChat.Contracts.Features.Integrations.Telegram;
using SuperChat.Domain.Features.Auth;
using SuperChat.Domain.Features.Integrations.Telegram;
using SuperChat.Infrastructure.Features.Integrations.Telegram.Userbot;
using SuperChat.Infrastructure.Shared.Persistence;

namespace SuperChat.Infrastructure.Features.Integrations.Telegram;

public sealed class TelegramConnectionService(
    IDbContextFactory<SuperChatDbContext> dbContextFactory,
    TelegramUserbotClient userbotClient,
    IOptions<PilotOptions> pilotOptions,
    TimeProvider timeProvider,
    ILogger<TelegramConnectionService> logger) : ITelegramConnectionService
{
    public async Task<TelegramConnection> StartAsync(AppUser user, CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await FindOrCreateAsync(dbContext, user.Id, cancellationToken);

        // If the user is already connected, keep the live session and ignore the
        // accidental re-click. This protects against the UI calling /connect after
        // a successful login flow finished.
        if (entity.State is TelegramConnectionState.Connected
            or TelegramConnectionState.LoginAwaitingCode
            or TelegramConnectionState.LoginAwaitingPassword)
        {
            return entity.ToDomain();
        }

        entity.State = TelegramConnectionState.LoginAwaitingPhone;
        entity.UpdatedAt = timeProvider.GetUtcNow();
        await dbContext.SaveChangesAsync(cancellationToken);
        return entity.ToDomain();
    }

    public async Task<TelegramConnection> ReconnectAsync(AppUser user, CancellationToken cancellationToken)
    {
        await DisconnectAsync(user.Id, cancellationToken);
        return await ForceStartAsync(user.Id, cancellationToken);
    }

    public Task<TelegramConnection> StartChatLoginAsync(AppUser user, CancellationToken cancellationToken)
        => StartAsync(user, cancellationToken);

    private Task<TelegramConnection> ForceStartAsync(Guid userId, CancellationToken cancellationToken)
        => TransitionAsync(userId, TelegramConnectionState.LoginAwaitingPhone, cancellationToken);

    public async Task<TelegramConnection> SubmitLoginInputAsync(AppUser user, string input, CancellationToken cancellationToken)
    {
        if (pilotOptions.Value.DevSeedSampleData)
        {
            return await SubmitDevelopmentLoginInputAsync(user, input, cancellationToken);
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await FindOrCreateAsync(dbContext, user.Id, cancellationToken);

        switch (entity.State)
        {
            case TelegramConnectionState.LoginAwaitingCode:
            {
                var code = input.Trim();
                if (code.Length == 0)
                {
                    return entity.ToDomain();
                }

                var result = await userbotClient.SubmitCodeAsync(user.Id, code, cancellationToken);
                entity.State = result.Status switch
                {
                    TelegramUserbotConnectStatus.Connected => TelegramConnectionState.Connected,
                    TelegramUserbotConnectStatus.AwaitingPassword => TelegramConnectionState.LoginAwaitingPassword,
                    TelegramUserbotConnectStatus.AwaitingCode => TelegramConnectionState.LoginAwaitingCode,
                    TelegramUserbotConnectStatus.Failed => TelegramConnectionState.Error,
                    _ => TelegramConnectionState.Error
                };
                break;
            }

            case TelegramConnectionState.LoginAwaitingPassword:
            {
                var password = input;
                if (string.IsNullOrEmpty(password))
                {
                    return entity.ToDomain();
                }

                var status = await userbotClient.SubmitPasswordAsync(user.Id, password, cancellationToken);
                entity.State = status switch
                {
                    TelegramUserbotConnectStatus.Connected => TelegramConnectionState.Connected,
                    TelegramUserbotConnectStatus.Failed => TelegramConnectionState.Error,
                    _ => TelegramConnectionState.Error
                };
                break;
            }

            default:
            {
                var phone = NormalizePhoneNumber(input);
                if (phone.Length == 0)
                {
                    logger.LogWarning("Rejected invalid phone number for user {UserId}: input contained no digits.", user.Id);
                    return entity.ToDomain();
                }

                var result = await userbotClient.StartConnectAsync(user.Id, phone, cancellationToken);
                entity.State = result.Success
                    ? TelegramConnectionState.LoginAwaitingCode
                    : TelegramConnectionState.Error;
                break;
            }
        }

        entity.UpdatedAt = timeProvider.GetUtcNow();
        await dbContext.SaveChangesAsync(cancellationToken);
        return entity.ToDomain();
    }

    public Task<TelegramConnection> CompleteDevelopmentConnectionAsync(AppUser user, CancellationToken cancellationToken)
        => TransitionAsync(user.Id, TelegramConnectionState.Connected, cancellationToken);

    public async Task<TelegramConnection> GetStatusAsync(Guid userId, CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await FindOrCreateAsync(dbContext, userId, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        return entity.ToDomain();
    }

    public async Task DisconnectAsync(Guid userId, CancellationToken cancellationToken)
    {
        if (!pilotOptions.Value.DevSeedSampleData)
        {
            try
            {
                await userbotClient.DisconnectAsync(userId, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "Failed to disconnect Telegram userbot session for user {UserId}.", userId);
            }
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await FindOrCreateAsync(dbContext, userId, cancellationToken);
        entity.State = TelegramConnectionState.Disconnected;
        entity.UpdatedAt = timeProvider.GetUtcNow();
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<TelegramConnection>> GetReadyForDevelopmentSyncAsync(CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var connections = await dbContext.TelegramConnections
            .AsNoTracking()
            .Where(item => item.State == TelegramConnectionState.Connected && item.DevelopmentSeededAt == null)
            .OrderBy(item => item.UpdatedAt)
            .ToListAsync(cancellationToken);

        return connections.Select(item => item.ToDomain()).ToList();
    }

    public async Task MarkSynchronizedAsync(Guid userId, DateTimeOffset synchronizedAt, CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await FindOrCreateAsync(dbContext, userId, cancellationToken);
        entity.LastSyncedAt = synchronizedAt;
        entity.UpdatedAt = synchronizedAt;

        if (pilotOptions.Value.DevSeedSampleData)
        {
            entity.DevelopmentSeededAt = synchronizedAt;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task MarkRevokedAsync(Guid userId, string reason, CancellationToken cancellationToken)
    {
        // Sidecar обнаружил, что Telegram отозвал auth_key (resume failed
        // или периодический probe). Не сбрасываем LastSyncedAt — пусть UI
        // помнит, когда последний раз нормально синкались, это полезно
        // при разборе «когда всё развалилось».
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await FindOrCreateAsync(dbContext, userId, cancellationToken);

        var previousState = entity.State;
        if (previousState != TelegramConnectionState.Revoked)
        {
            // Логируем только реальные переходы, чтобы повторные probe-вызовы
            // health-check не засоряли Loki.
            logger.LogWarning(
                "Telegram session revoked for user {UserId}: previousState={PreviousState}, reason={Reason}.",
                userId,
                previousState,
                reason);
        }

        entity.State = TelegramConnectionState.Revoked;
        entity.UpdatedAt = timeProvider.GetUtcNow();
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<TelegramConnection> SubmitDevelopmentLoginInputAsync(
        AppUser user,
        string input,
        CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await FindOrCreateAsync(dbContext, user.Id, cancellationToken);

        switch (entity.State)
        {
            case TelegramConnectionState.LoginAwaitingCode:
            case TelegramConnectionState.LoginAwaitingPassword:
            {
                if (string.IsNullOrWhiteSpace(input))
                {
                    return entity.ToDomain();
                }

                entity.State = TelegramConnectionState.Connected;
                break;
            }

            default:
            {
                if (NormalizePhoneNumber(input).Length == 0)
                {
                    return entity.ToDomain();
                }

                entity.State = TelegramConnectionState.LoginAwaitingCode;
                break;
            }
        }

        entity.UpdatedAt = timeProvider.GetUtcNow();
        await dbContext.SaveChangesAsync(cancellationToken);
        return entity.ToDomain();
    }

    private async Task<TelegramConnection> TransitionAsync(
        Guid userId,
        TelegramConnectionState state,
        CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await FindOrCreateAsync(dbContext, userId, cancellationToken);
        entity.State = state;
        entity.UpdatedAt = timeProvider.GetUtcNow();
        await dbContext.SaveChangesAsync(cancellationToken);
        return entity.ToDomain();
    }

    private async Task<TelegramConnectionEntity> FindOrCreateAsync(
        SuperChatDbContext dbContext,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var existing = await dbContext.TelegramConnections.SingleOrDefaultAsync(item => item.UserId == userId, cancellationToken);
        if (existing is not null)
        {
            return existing;
        }

        var created = new TelegramConnectionEntity
        {
            UserId = userId,
            State = TelegramConnectionState.NotStarted,
            UpdatedAt = timeProvider.GetUtcNow()
        };

        dbContext.TelegramConnections.Add(created);
        return created;
    }

    /// <summary>Strips formatting characters from a phone number, keeping only leading '+' and digits.</summary>
    internal static string NormalizePhoneNumber(string input)
    {
        var span = input.AsSpan().Trim();
        var buffer = new char[span.Length];
        var pos = 0;

        for (var i = 0; i < span.Length; i++)
        {
            if (char.IsAsciiDigit(span[i]) || (i == 0 && span[i] == '+'))
            {
                buffer[pos++] = span[i];
            }
        }

        return pos > 0 ? new string(buffer, 0, pos) : string.Empty;
    }
}
