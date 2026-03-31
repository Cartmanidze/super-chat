using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SuperChat.Contracts.Features.Auth;
using SuperChat.Contracts.Features.Integrations.Telegram;
using SuperChat.Infrastructure.Features.Integrations.Matrix;

namespace SuperChat.Infrastructure.Diagnostics;

public sealed class BridgeHealthCheck(
    MatrixApiClient matrixApiClient,
    IOptions<TelegramBridgeOptions> bridgeOptions,
    IOptions<PilotOptions> pilotOptions,
    ILogger<BridgeHealthCheck> logger) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        if (pilotOptions.Value.DevSeedSampleData)
        {
            return HealthCheckResult.Healthy("Bridge check skipped (development seed mode).");
        }

        // 1. Synapse reachability — unauthenticated GET /_matrix/client/versions
        try
        {
            var reachable = await matrixApiClient.IsSynapseReachableAsync(cancellationToken);
            if (!reachable)
            {
                return HealthCheckResult.Unhealthy("Synapse homeserver did not respond.");
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Synapse reachability check failed.");
            return HealthCheckResult.Unhealthy("Synapse homeserver is unreachable.", ex);
        }

        // 2. Bridge bot profile — unauthenticated GET /_matrix/client/v3/profile/{botUserId}
        var botUserId = bridgeOptions.Value.BotUserId;
        if (string.IsNullOrWhiteSpace(botUserId))
        {
            return HealthCheckResult.Degraded("Telegram bridge bot user ID is not configured.");
        }

        try
        {
            var botExists = await matrixApiClient.DoesUserProfileExistAsync(botUserId, cancellationToken);
            if (!botExists)
            {
                return HealthCheckResult.Degraded(
                    $"Bridge bot profile '{botUserId}' not found in Synapse. Bridge may not be running.");
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Bridge bot profile check failed for {BotUserId}.", botUserId);
            return HealthCheckResult.Degraded("Unable to verify bridge bot profile.", ex);
        }

        return HealthCheckResult.Healthy("Synapse reachable, bridge bot profile exists.");
    }
}
