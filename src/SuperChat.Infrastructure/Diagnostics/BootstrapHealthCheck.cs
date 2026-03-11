using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using SuperChat.Contracts.Configuration;

namespace SuperChat.Infrastructure.Health;

public sealed class BootstrapHealthCheck(IOptions<PilotOptions> pilotOptions) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var options = pilotOptions.Value;

        if (options.AllowedEmails.Length == 0)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy("No pilot emails configured."));
        }

        return Task.FromResult(HealthCheckResult.Healthy($"Ready for {options.AllowedEmails.Length} pilot users."));
    }
}
