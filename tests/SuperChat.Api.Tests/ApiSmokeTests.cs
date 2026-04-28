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
using Microsoft.Extensions.Hosting;
using SuperChat.Domain.Features.Intelligence;
using SuperChat.Infrastructure.Shared.Persistence;

namespace SuperChat.Api.Tests;

public sealed class ApiSmokeTests : IClassFixture<ApiTestApplicationFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly ApiTestApplicationFactory _factory;
    private static string? _cachedAccessToken;

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
        Assert.Equal("{\"status\":\"ok\"}", content);
    }

    [Fact]
    public async Task HealthEndpoint_ReturnsGeneratedCorrelationIdHeader_WhenMissing()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/v1/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.TryGetValues("X-Correlation-ID", out var values));
        Assert.False(string.IsNullOrWhiteSpace(values!.Single()));
    }

    [Fact]
    public async Task HealthEndpoint_PreservesIncomingCorrelationIdHeader()
    {
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Correlation-ID", "test-correlation-id");

        var response = await client.GetAsync("/api/v1/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.TryGetValues("X-Correlation-ID", out var values));
        Assert.Equal("test-correlation-id", values!.Single());
    }

    [Fact]
    public async Task MetricsEndpoint_ExposesApplicationMetrics()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/metrics");
        var content = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("superchat_pipeline_commands_total", content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task OpenApiEndpoints_ExposeJsonDocument_AndScalarUi()
    {
        using var client = _factory.CreateClient();

        var openApiResponse = await client.GetAsync("/openapi/v1.json");
        var openApiContent = await openApiResponse.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, openApiResponse.StatusCode);
        Assert.Contains("\"openapi\"", openApiContent, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("/api/v1/health", openApiContent, StringComparison.Ordinal);
        Assert.Contains("/api/v1/work-items/meetings", openApiContent, StringComparison.Ordinal);

        var docsResponse = await client.GetAsync("/docs");
        var docsContent = await docsResponse.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, docsResponse.StatusCode);
        Assert.Contains("Scalar", docsContent, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AuthFlow_SendsCodeAndVerifiesForBearerToken_AndReturnsProfile()
    {
        using var client = _factory.CreateClient();

        var sendResponse = await client.PostAsJsonAsync("/api/v1/auth/send-code", new
        {
            email = "pilot@example.com"
        });

        Assert.Equal(HttpStatusCode.Accepted, sendResponse.StatusCode);

        var codeSender = _factory.Services.GetRequiredService<CapturingApiCodeSender>();
        var code = codeSender.LastCode!;

        var verifyResponse = await client.PostAsJsonAsync("/api/v1/auth/verify-code", new
        {
            email = "pilot@example.com",
            code
        });
        var session = await verifyResponse.Content.ReadFromJsonAsync<SessionTokenEnvelope>(JsonOptions);

        Assert.Equal(HttpStatusCode.OK, verifyResponse.StatusCode);
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
    public async Task AuthFlow_StoresUserTimeZoneFromVerifyRequest()
    {
        using var client = _factory.CreateClient();

        var sendResponse = await client.PostAsJsonAsync("/api/v1/auth/send-code", new
        {
            email = "pilot@example.com"
        });

        Assert.Equal(HttpStatusCode.Accepted, sendResponse.StatusCode);

        var codeSender = _factory.Services.GetRequiredService<CapturingApiCodeSender>();
        var code = codeSender.LastCode!;

        var verifyResponse = await client.PostAsJsonAsync("/api/v1/auth/verify-code", new
        {
            email = "pilot@example.com",
            code,
            timeZoneId = "Asia/Omsk"
        });

        Assert.Equal(HttpStatusCode.OK, verifyResponse.StatusCode);

        await using var dbContext = await CreateDbContextAsync();
        var timeZoneId = await dbContext.AppUsers
            .Where(item => item.Email == "pilot@example.com")
            .Select(item => item.TimeZoneId)
            .SingleAsync();

        Assert.Equal("Asia/Omsk", timeZoneId);
    }

    [Fact]
    public async Task AuthFlow_RejectsMissingEmail_WithValidationProblem()
    {
        using var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/auth/send-code", new
        {
            email = ""
        });

        var payload = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("\"email\"", payload, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Email is required.", payload, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TelegramEndpoints_CanConnect_Reconnect_AndDisconnect()
    {
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await GetAccessTokenAsync(client));

        var connectResponse = await client.PostAsync("/api/v1/integrations/telegram/connect", content: null);
        var connect = await connectResponse.Content.ReadFromJsonAsync<TelegramConnectionEnvelope>(JsonOptions);

        Assert.Equal(HttpStatusCode.OK, connectResponse.StatusCode);
        Assert.NotNull(connect);
        Assert.Equal("Pending", connect!.State);
        Assert.Equal("phone", connect.ChatLoginStep);
        Assert.True(connect.RequiresAction);

        var reconnectResponse = await client.PostAsync("/api/v1/integrations/telegram/reconnect", content: null);
        var reconnect = await reconnectResponse.Content.ReadFromJsonAsync<TelegramConnectionEnvelope>(JsonOptions);

        Assert.Equal(HttpStatusCode.OK, reconnectResponse.StatusCode);
        Assert.NotNull(reconnect);
        Assert.Equal("Pending", reconnect!.State);
        Assert.Equal("phone", reconnect.ChatLoginStep);
        Assert.True(reconnect.RequiresAction);

        var disconnectResponse = await client.DeleteAsync("/api/v1/integrations/telegram");
        var disconnect = await disconnectResponse.Content.ReadFromJsonAsync<TelegramConnectionEnvelope>(JsonOptions);

        Assert.Equal(HttpStatusCode.OK, disconnectResponse.StatusCode);
        Assert.NotNull(disconnect);
        Assert.Equal("Disconnected", disconnect!.State);
    }

    [Fact]
    public async Task TelegramEndpoints_DoNotExposeWebLoginUrl()
    {
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await GetAccessTokenAsync(client));

        var response = await client.PostAsync("/api/v1/integrations/telegram/connect", content: null);
        var payload = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.DoesNotContain("\"webLoginUrl\"", payload, StringComparison.OrdinalIgnoreCase);
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

        var emailStatusResponse = await client.GetAsync("/api/v1/integrations/email");
        var emailConnectResponse = await client.PostAsync("/api/v1/integrations/email/connect", content: null);

        Assert.Equal(HttpStatusCode.NotImplemented, emailStatusResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotImplemented, emailConnectResponse.StatusCode);
    }

    [Fact]
    public async Task WorkItemEndpoints_RejectInvalidMeetingId_WithNotFound()
    {
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await GetAccessTokenAsync(client));

        var response = await client.PostAsync("/api/v1/work-items/meetings/not-a-work-item-id/complete", content: null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task WorkItemEndpoints_Dismiss_RemovesMeeting()
    {
        using var client = _factory.CreateClient();
        var accessToken = await GetAccessTokenAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var userId = await GetUserIdAsync("pilot@example.com");
        var meetingId = Guid.NewGuid();
        var scheduledFor = DateTimeOffset.UtcNow.AddHours(2);

        await using (var dbContext = await CreateDbContextAsync())
        {
            dbContext.Meetings.Add(new MeetingEntity
            {
                Id = meetingId,
                UserId = userId,
                Title = "Product sync",
                Summary = "Meet product team in two hours.",
                ExternalChatId = "!team:matrix.localhost",
                SourceEventId = "$evt-api-meeting",
                ObservedAt = scheduledFor.AddHours(-1),
                ScheduledFor = scheduledFor,
                Confidence = 0.84,
                CreatedAt = scheduledFor.AddHours(-1),
                UpdatedAt = scheduledFor.AddHours(-1)
            });

            await dbContext.SaveChangesAsync();
        }

        var dismissResponse = await client.PostAsync(
            $"/api/v1/work-items/meetings/{meetingId}/dismiss",
            content: null);

        Assert.Equal(HttpStatusCode.NoContent, dismissResponse.StatusCode);

        var meetingsResponse = await client.GetAsync("/api/v1/work-items/meetings");
        var meetingsPayload = await meetingsResponse.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, meetingsResponse.StatusCode);
        Assert.DoesNotContain("Product sync", meetingsPayload, StringComparison.Ordinal);

        await using var verificationContext = await CreateDbContextAsync();
        var meeting = await verificationContext.Meetings.SingleAsync(item => item.Id == meetingId);

        Assert.NotNull(meeting.ResolvedAt);
        Assert.Equal("dismissed", meeting.ResolutionKind);
        Assert.Equal("manual", meeting.ResolutionSource);
    }

    [Fact]
    public async Task WorkItemEndpoints_ConfirmAndUnconfirmMeeting()
    {
        using var client = _factory.CreateClient();
        var accessToken = await GetAccessTokenAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var userId = await GetUserIdAsync("pilot@example.com");
        var meetingId = Guid.NewGuid();
        var scheduledFor = DateTimeOffset.UtcNow.AddHours(2);

        await using (var dbContext = await CreateDbContextAsync())
        {
            dbContext.Meetings.Add(new MeetingEntity
            {
                Id = meetingId,
                UserId = userId,
                Title = "Interview sync",
                Summary = "Interview at 18:00.",
                ExternalChatId = "!team:matrix.localhost",
                SourceEventId = "$evt-api-confirm",
                ObservedAt = scheduledFor.AddHours(-1),
                ScheduledFor = scheduledFor,
                Confidence = 0.84,
                Status = MeetingStatus.PendingConfirmation,
                CreatedAt = scheduledFor.AddHours(-1),
                UpdatedAt = scheduledFor.AddHours(-1)
            });

            await dbContext.SaveChangesAsync();
        }

        var confirmResponse = await client.PostAsync(
            $"/api/v1/work-items/meetings/{meetingId}/confirm",
            content: null);

        Assert.Equal(HttpStatusCode.NoContent, confirmResponse.StatusCode);

        await using (var verificationContext = await CreateDbContextAsync())
        {
            var confirmedMeeting = await verificationContext.Meetings.SingleAsync(item => item.Id == meetingId);
            Assert.Equal(MeetingStatus.Confirmed, confirmedMeeting.Status);
            Assert.Null(confirmedMeeting.ResolvedAt);
        }

        var unconfirmResponse = await client.PostAsync(
            $"/api/v1/work-items/meetings/{meetingId}/unconfirm",
            content: null);

        Assert.Equal(HttpStatusCode.NoContent, unconfirmResponse.StatusCode);

        await using var verificationContextAfterUnconfirm = await CreateDbContextAsync();
        var unconfirmedMeeting = await verificationContextAfterUnconfirm.Meetings.SingleAsync(item => item.Id == meetingId);
        Assert.Equal(MeetingStatus.PendingConfirmation, unconfirmedMeeting.Status);
        Assert.Null(unconfirmedMeeting.ResolvedAt);
    }

    [Fact]
    public async Task WorkItemEndpoints_ListUpcomingMeetings()
    {
        using var client = _factory.CreateClient();
        var accessToken = await GetAccessTokenAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var userId = await GetUserIdAsync("pilot@example.com");
        var meetingId = Guid.NewGuid();
        var scheduledFor = DateTimeOffset.UtcNow.AddHours(3);

        await using (var dbContext = await CreateDbContextAsync())
        {
            dbContext.Meetings.Add(new MeetingEntity
            {
                Id = meetingId,
                UserId = userId,
                Title = "Catalog planning sync",
                Summary = "Meet product team in three hours.",
                ExternalChatId = "!team:matrix.localhost",
                SourceEventId = "$evt-api-common-meeting",
                ObservedAt = DateTimeOffset.UtcNow.AddMinutes(-5),
                ScheduledFor = scheduledFor,
                Confidence = 0.84,
                CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-5),
                UpdatedAt = DateTimeOffset.UtcNow.AddMinutes(-5)
            });

            await dbContext.SaveChangesAsync();
        }

        var meetingsResponse = await client.GetAsync("/api/v1/work-items/meetings");
        var meetingsPayload = await meetingsResponse.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, meetingsResponse.StatusCode);
        Assert.Contains("Catalog planning sync", meetingsPayload, StringComparison.Ordinal);
        Assert.DoesNotContain("Prepare deck", meetingsPayload, StringComparison.Ordinal);
        Assert.DoesNotContain("Reply to Marina", meetingsPayload, StringComparison.Ordinal);
    }

    private async Task<string> GetAccessTokenAsync(HttpClient client)
    {
        if (_cachedAccessToken is not null)
        {
            return _cachedAccessToken;
        }

        var sendResponse = await client.PostAsJsonAsync("/api/v1/auth/send-code", new
        {
            email = "pilot@example.com"
        });

        sendResponse.EnsureSuccessStatusCode();

        var codeSender = _factory.Services.GetRequiredService<CapturingApiCodeSender>();
        var code = codeSender.LastCode!;

        var verifyResponse = await client.PostAsJsonAsync("/api/v1/auth/verify-code", new
        {
            email = "pilot@example.com",
            code
        });

        var session = await verifyResponse.Content.ReadFromJsonAsync<SessionTokenEnvelope>(JsonOptions);
        _cachedAccessToken = session!.AccessToken;
        return _cachedAccessToken;
    }

    private async Task<Guid> GetUserIdAsync(string email)
    {
        await using var dbContext = await CreateDbContextAsync();
        return await dbContext.AppUsers
            .Where(item => item.Email == email)
            .Select(item => item.Id)
            .SingleAsync();
    }

    private async Task<SuperChatDbContext> CreateDbContextAsync()
    {
        var dbContextFactory = _factory.Services.GetRequiredService<IDbContextFactory<SuperChatDbContext>>();
        return await dbContextFactory.CreateDbContextAsync();
    }

    private sealed record SessionTokenEnvelope(string AccessToken, string TokenType, DateTimeOffset ExpiresAt);

    private sealed record MeEnvelope(Guid Id, string Email, string? MatrixUserId, string TelegramState);

    private sealed record TelegramConnectionEnvelope(
        string State,
        string? MatrixUserId,
        Uri? WebLoginUrl,
        string? ChatLoginStep,
        DateTimeOffset? LastSyncedAt,
        bool RequiresAction);

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
                ["SuperChat:ApiSessionDays"] = "30",
                ["SuperChat:VerificationCodeMinutes"] = "10",
                ["SuperChat:MaxVerificationAttempts"] = "5",
                ["SuperChat:DevSeedSampleData"] = "true"
            });
        });
        builder.ConfigureServices(services =>
        {
            services.RemoveAll(typeof(IDbContextFactory<SuperChatDbContext>));
            services.RemoveAll(typeof(DbContextOptions<SuperChatDbContext>));
            services.AddDbContextFactory<SuperChatDbContext>(options => options.UseSqlite($"Data Source={_databasePath}"));
            services.RemoveAll(typeof(SuperChat.Contracts.Features.Auth.IVerificationCodeSender));
            services.AddSingleton<CapturingApiCodeSender>();
            services.AddSingleton<SuperChat.Contracts.Features.Auth.IVerificationCodeSender>(sp => sp.GetRequiredService<CapturingApiCodeSender>());
        });
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);

        using var scope = host.Services.CreateScope();
        var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<SuperChatDbContext>>();
        using var dbContext = dbContextFactory.CreateDbContext();
        dbContext.Database.EnsureCreated();

        if (!dbContext.PilotInvites.Any(item => item.Email == "pilot@example.com"))
        {
            dbContext.PilotInvites.Add(new PilotInviteEntity
            {
                Email = "pilot@example.com",
                InvitedBy = "test",
                InvitedAt = DateTimeOffset.UtcNow,
                IsActive = true
            });

            dbContext.SaveChanges();
        }

        return host;
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
        catch (IOException)
        {
        }
    }
}

public sealed class CapturingApiCodeSender : SuperChat.Contracts.Features.Auth.IVerificationCodeSender
{
    public string? LastCode { get; private set; }

    public Task SendAsync(string email, string code, CancellationToken cancellationToken)
    {
        LastCode = code;
        return Task.CompletedTask;
    }
}
