using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SuperChat.Contracts.Configuration;
using SuperChat.Infrastructure.Persistence;

namespace SuperChat.Infrastructure.HostedServices;

public sealed class PersistenceInitializationHostedService(
    IDbContextFactory<SuperChatDbContext> dbContextFactory,
    IOptions<PersistenceOptions> persistenceOptions,
    IOptions<PilotOptions> pilotOptions,
    ILogger<PersistenceInitializationHostedService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!persistenceOptions.Value.AutoInitialize)
        {
            logger.LogInformation("Persistence auto-initialization is disabled.");
            return;
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await dbContext.Database.EnsureCreatedAsync(cancellationToken);

        var configuredInvites = pilotOptions.Value.AllowedEmails
            .Select(email => email.Trim().ToLowerInvariant())
            .Where(email => !string.IsNullOrWhiteSpace(email))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (configuredInvites.Count == 0)
        {
            return;
        }

        var existingInvites = await dbContext.PilotInvites
            .Where(item => configuredInvites.Contains(item.Email))
            .Select(item => item.Email)
            .ToListAsync(cancellationToken);

        var missingInvites = configuredInvites
            .Except(existingInvites, StringComparer.OrdinalIgnoreCase)
            .Select(email => new PilotInviteEntity
            {
                Email = email,
                InvitedBy = "bootstrap",
                InvitedAt = DateTimeOffset.UtcNow,
                IsActive = true
            })
            .ToList();

        if (missingInvites.Count == 0)
        {
            return;
        }

        dbContext.PilotInvites.AddRange(missingInvites);
        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Seeded {InviteCount} pilot invites into persistence store.", missingInvites.Count);
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
