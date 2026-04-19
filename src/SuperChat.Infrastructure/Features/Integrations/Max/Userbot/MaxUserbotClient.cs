using Microsoft.Extensions.Logging;

namespace SuperChat.Infrastructure.Features.Integrations.Max.Userbot;

/// <summary>
/// Placeholder client for the future Max userbot sidecar.
/// Real endpoints mirror the Telegram userbot shape; until the protocol is reverse-engineered
/// this class just returns NotImplemented to keep DI wired and give callers a stable surface.
/// </summary>
public sealed class MaxUserbotClient(
    HttpClient httpClient,
    ILogger<MaxUserbotClient> logger)
{
    public Task<bool> HealthcheckAsync(CancellationToken cancellationToken)
    {
        logger.LogDebug("Max userbot healthcheck stub called. BaseAddress={BaseAddress}.", httpClient.BaseAddress);
        return Task.FromResult(false);
    }
}
