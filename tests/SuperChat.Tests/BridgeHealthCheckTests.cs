using System.Net;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SuperChat.Contracts.Features.Auth;
using SuperChat.Contracts.Features.Integrations.Telegram;
using SuperChat.Infrastructure.Diagnostics;
using MatrixApiClient = SuperChat.Infrastructure.Features.Integrations.Matrix.MatrixApiClient;

namespace SuperChat.Tests;

public sealed class BridgeHealthCheckTests
{
    [Fact]
    public async Task ReturnsHealthy_WhenDevSeedMode()
    {
        var check = CreateHealthCheck(
            _ => throw new InvalidOperationException("Should not be called"),
            devSeed: true);

        var result = await check.CheckHealthAsync(CreateContext(), CancellationToken.None);

        Assert.Equal(HealthStatus.Healthy, result.Status);
        Assert.Contains("development seed mode", result.Description);
    }

    [Fact]
    public async Task ReturnsUnhealthy_WhenSynapseUnreachable()
    {
        var check = CreateHealthCheck(
            _ => throw new HttpRequestException("Connection refused"));

        var result = await check.CheckHealthAsync(CreateContext(), CancellationToken.None);

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.Contains("unreachable", result.Description);
    }

    [Fact]
    public async Task ReturnsDegraded_WhenBotProfileNotFound()
    {
        var check = CreateHealthCheck(request =>
        {
            if (request.RequestUri!.AbsolutePath.Contains("/versions"))
                return new HttpResponseMessage(HttpStatusCode.OK);

            // Profile returns 404
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        var result = await check.CheckHealthAsync(CreateContext(), CancellationToken.None);

        Assert.Equal(HealthStatus.Degraded, result.Status);
        Assert.Contains("not found", result.Description);
    }

    [Fact]
    public async Task ReturnsHealthy_WhenSynapseAndBotProfileOk()
    {
        var check = CreateHealthCheck(_ => new HttpResponseMessage(HttpStatusCode.OK));

        var result = await check.CheckHealthAsync(CreateContext(), CancellationToken.None);

        Assert.Equal(HealthStatus.Healthy, result.Status);
    }

    [Fact]
    public async Task ReturnsDegraded_WhenBotUserIdNotConfigured()
    {
        var check = CreateHealthCheck(
            _ => new HttpResponseMessage(HttpStatusCode.OK),
            botUserId: "");

        var result = await check.CheckHealthAsync(CreateContext(), CancellationToken.None);

        Assert.Equal(HealthStatus.Degraded, result.Status);
        Assert.Contains("not configured", result.Description);
    }

    private static BridgeHealthCheck CreateHealthCheck(
        Func<HttpRequestMessage, HttpResponseMessage> responseFactory,
        bool devSeed = false,
        string botUserId = "@telegrambot:matrix.localhost")
    {
        var handler = new TestHandler(responseFactory);
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:8008") };
        var matrixOptions = Options.Create(new SuperChat.Contracts.Features.Integrations.Matrix.MatrixOptions());
        var matrixApiClient = new MatrixApiClient(httpClient, matrixOptions, NullLogger<MatrixApiClient>.Instance);
        var bridgeOptions = Options.Create(new TelegramBridgeOptions { BotUserId = botUserId });
        var pilotOptions = Options.Create(new PilotOptions { DevSeedSampleData = devSeed });

        return new BridgeHealthCheck(matrixApiClient, bridgeOptions, pilotOptions, NullLogger<BridgeHealthCheck>.Instance);
    }

    private static HealthCheckContext CreateContext() => new()
    {
        Registration = new HealthCheckRegistration("bridge", _ => null!, null, null)
    };

    private sealed class TestHandler(Func<HttpRequestMessage, HttpResponseMessage> responseFactory) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(responseFactory(request));
        }
    }
}
