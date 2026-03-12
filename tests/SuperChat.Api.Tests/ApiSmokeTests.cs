using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SuperChat.Infrastructure.Persistence;

namespace SuperChat.Api.Tests;

public sealed class ApiSmokeTests : IClassFixture<ApiTestApplicationFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly ApiTestApplicationFactory _factory;

    public ApiSmokeTests(ApiTestApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task HealthEndpoint_ReturnsOkPayload()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/v1/health");
        var content = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("\"status\":\"ok\"", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("\"activeSessions\":", content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AuthFlow_ExchangesMagicLinkForBearerToken_AndReturnsProfile()
    {
        using var client = _factory.CreateClient();

        var requestResponse = await client.PostAsJsonAsync("/api/v1/auth/magic-links", new
        {
            email = "pilot@example.com"
        });
        var requestBody = await requestResponse.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.Accepted, requestResponse.StatusCode);

        using var requestJson = JsonDocument.Parse(requestBody);
        var devLink = requestJson.RootElement.GetProperty("developmentLink").GetString();
        Assert.False(string.IsNullOrWhiteSpace(devLink));

        var token = ExtractToken(devLink!);
        var exchangeResponse = await client.PostAsJsonAsync("/api/v1/auth/token-exchange", new
        {
            token
        });
        var session = await exchangeResponse.Content.ReadFromJsonAsync<SessionTokenEnvelope>(JsonOptions);

        Assert.Equal(HttpStatusCode.OK, exchangeResponse.StatusCode);
        Assert.NotNull(session);
        Assert.False(string.IsNullOrWhiteSpace(session!.AccessToken));

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);

        var meResponse = await client.GetAsync("/api/v1/me");
        var me = await meResponse.Content.ReadFromJsonAsync<MeEnvelope>(JsonOptions);

        Assert.Equal(HttpStatusCode.OK, meResponse.StatusCode);
        Assert.NotNull(me);
        Assert.Equal("pilot@example.com", me!.Email);
    }

    [Fact]
    public async Task TelegramEndpoints_CanConnect_AndDisconnect()
    {
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await GetAccessTokenAsync(client));

        var connectResponse = await client.PostAsync("/api/v1/integrations/telegram/connect", content: null);
        var connect = await connectResponse.Content.ReadFromJsonAsync<TelegramConnectionEnvelope>(JsonOptions);

        Assert.Equal(HttpStatusCode.OK, connectResponse.StatusCode);
        Assert.NotNull(connect);
        Assert.Equal("Connected", connect!.State);

        var disconnectResponse = await client.DeleteAsync("/api/v1/integrations/telegram");
        var disconnect = await disconnectResponse.Content.ReadFromJsonAsync<TelegramConnectionEnvelope>(JsonOptions);

        Assert.Equal(HttpStatusCode.OK, disconnectResponse.StatusCode);
        Assert.NotNull(disconnect);
        Assert.Equal("Disconnected", disconnect!.State);
    }

    [Fact]
    public async Task IntegrationEndpoints_ListConnections_AndRejectUnimplementedProvider()
    {
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await GetAccessTokenAsync(client));

        var listBeforeResponse = await client.GetAsync("/api/v1/integrations");
        var listBefore = await listBeforeResponse.Content.ReadFromJsonAsync<List<IntegrationConnectionEnvelope>>(JsonOptions);

        Assert.Equal(HttpStatusCode.OK, listBeforeResponse.StatusCode);
        Assert.NotNull(listBefore);
        Assert.Single(listBefore!);
        Assert.Equal("telegram", listBefore[0].Provider);
        Assert.Equal("NotStarted", listBefore[0].State);

        var emailStatusResponse = await client.GetAsync("/api/v1/integrations/email");
        var emailConnectResponse = await client.PostAsync("/api/v1/integrations/email/connect", content: null);

        Assert.Equal(HttpStatusCode.NotImplemented, emailStatusResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotImplemented, emailConnectResponse.StatusCode);
    }

    private static string ExtractToken(string developmentLink)
    {
        var uri = new Uri(developmentLink);
        var tokenPart = uri.Query.TrimStart('?')
            .Split('&', StringSplitOptions.RemoveEmptyEntries)
            .First(part => part.StartsWith("token=", StringComparison.OrdinalIgnoreCase));

        return Uri.UnescapeDataString(tokenPart["token=".Length..]);
    }

    private static async Task<string> GetAccessTokenAsync(HttpClient client)
    {
        var requestResponse = await client.PostAsJsonAsync("/api/v1/auth/magic-links", new
        {
            email = "pilot@example.com"
        });

        var requestBody = await requestResponse.Content.ReadAsStringAsync();
        using var requestJson = JsonDocument.Parse(requestBody);
        var devLink = requestJson.RootElement.GetProperty("developmentLink").GetString();
        var token = ExtractToken(devLink!);

        var exchangeResponse = await client.PostAsJsonAsync("/api/v1/auth/token-exchange", new
        {
            token
        });

        var session = await exchangeResponse.Content.ReadFromJsonAsync<SessionTokenEnvelope>(JsonOptions);
        return session!.AccessToken;
    }

    private sealed record SessionTokenEnvelope(string AccessToken, string TokenType, DateTimeOffset ExpiresAt);

    private sealed record MeEnvelope(Guid Id, string Email, string? MatrixUserId, string TelegramState);

    private sealed record TelegramConnectionEnvelope(string State, string? MatrixUserId, Uri? WebLoginUrl, DateTimeOffset? LastSyncedAt, bool RequiresAction);

    private sealed record IntegrationConnectionEnvelope(
        string Provider,
        string Transport,
        string State,
        string? MatrixUserId,
        Uri? ActionUrl,
        DateTimeOffset? LastSyncedAt,
        bool RequiresAction);
}

public sealed class ApiTestApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"superchat-api-{Guid.NewGuid():N}.db");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:SuperChatDb"] = $"Data Source={_databasePath}",
                ["Persistence:Provider"] = "Sqlite",
                ["SuperChat:AllowedEmails:0"] = "pilot@example.com",
                ["SuperChat:ApiSessionDays"] = "30",
                ["SuperChat:DevSeedSampleData"] = "true"
            });
        });
        builder.ConfigureServices(services =>
        {
            services.RemoveAll(typeof(IDbContextFactory<SuperChatDbContext>));
            services.RemoveAll(typeof(DbContextOptions<SuperChatDbContext>));
            services.AddDbContextFactory<SuperChatDbContext>(options => options.UseSqlite($"Data Source={_databasePath}"));
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (!disposing)
        {
            return;
        }

        try
        {
            if (File.Exists(_databasePath))
            {
                File.Delete(_databasePath);
            }
        }
        catch
        {
        }
    }
}
