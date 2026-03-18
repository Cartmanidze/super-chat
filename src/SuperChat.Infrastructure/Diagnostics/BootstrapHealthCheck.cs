using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using SuperChat.Contracts.Configuration;
using SuperChat.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace SuperChat.Infrastructure.Health;

public sealed class BootstrapHealthCheck(
    IDbContextFactory<SuperChatDbContext> dbContextFactory,
    IOptions<PilotOptions> pilotOptions) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var options = pilotOptions.Value;
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var activeInviteCount = await dbContext.PilotInvites.CountAsync(item => item.IsActive, cancellationToken);
        var configuredAdminCount = options.AdminEmails.Count(email => !string.IsNullOrWhiteSpace(email));

        if (activeInviteCount == 0 && configuredAdminCount == 0)
        {
            return HealthCheckResult.Unhealthy("No active invites or configured admins are available to bootstrap access.");
        }

        if (activeInviteCount == 0)
        {
            return HealthCheckResult.Healthy($"Ready for bootstrap via {configuredAdminCount} configured admin accounts.");
        }

        return HealthCheckResult.Healthy($"Ready for {activeInviteCount} invited users.");
    }
}
