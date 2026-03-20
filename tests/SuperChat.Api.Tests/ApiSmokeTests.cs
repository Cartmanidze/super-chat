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
using SuperChat.Contracts.Features.WorkItems;
using SuperChat.Domain.Features.Intelligence;
using SuperChat.Infrastructure.Shared.Persistence;

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
        Assert.Equal("{\"status\":\"ok\"}", content);
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
    public async Task AuthFlow_RejectsMissingEmail_WithValidationProblem()
    {
        using var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/auth/magic-links", new
        {
            email = ""
        });

        var payload = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("\"email\"", payload, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Email is required.", payload, StringComparison.Ordinal);
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
        Assert.True(
            string.Equals(listBefore[0].State, "NotStarted", StringComparison.Ordinal) ||
            string.Equals(listBefore[0].State, "Connected", StringComparison.Ordinal));

        var emailStatusResponse = await client.GetAsync("/api/v1/integrations/email");
        var emailConnectResponse = await client.PostAsync("/api/v1/integrations/email/connect", content: null);

        Assert.Equal(HttpStatusCode.NotImplemented, emailStatusResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotImplemented, emailConnectResponse.StatusCode);
    }

    [Fact]
    public async Task ChatEndpoint_ReturnsStructuredAnswer_AndValidatesLength()
    {
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await GetAccessTokenAsync(client));

        var connectResponse = await client.PostAsync("/api/v1/integrations/telegram/connect", content: null);
        Assert.Equal(HttpStatusCode.OK, connectResponse.StatusCode);

        var okResponse = await client.PostAsJsonAsync("/api/v1/chat/ask", new
        {
            templateId = "today",
            question = "Что для меня важно сегодня?"
        });

        var okPayload = await okResponse.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, okResponse.StatusCode);
        Assert.Contains("\"mode\":\"today\"", okPayload, StringComparison.OrdinalIgnoreCase);

        var badResponse = await client.PostAsJsonAsync("/api/v1/chat/ask", new
        {
            templateId = "custom",
            question = new string('x', 101)
        });

        Assert.Equal(HttpStatusCode.BadRequest, badResponse.StatusCode);
    }

    [Fact]
    public async Task ChatEndpoint_RejectsUnsupportedTemplate_WithTemplateIdValidationProblem()
    {
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await GetAccessTokenAsync(client));

        var response = await client.PostAsJsonAsync("/api/v1/chat/ask", new
        {
            templateId = "unknown-template",
            question = "Что важно?"
        });

        var payload = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("\"templateId\"", payload, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Unsupported chat template.", payload, StringComparison.Ordinal);
    }

    [Fact]
    public async Task WorkItemEndpoints_Complete_RemovesActiveWaitingItem()
    {
        using var client = _factory.CreateClient();
        var accessToken = await GetAccessTokenAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var userId = await GetUserIdAsync("pilot@example.com");
        var itemId = Guid.NewGuid();

        await using (var dbContext = await CreateDbContextAsync())
        {
            dbContext.WorkItems.Add(new WorkItemEntity
            {
                Id = itemId,
                UserId = userId,
                Kind = ExtractedItemKind.WaitingOn,
                Title = "Reply to Marina",
                Summary = "Need to answer Marina today.",
                SourceRoom = "!sales:matrix.localhost",
                SourceEventId = "$evt-api-waiting",
                Person = "Marina",
                ObservedAt = DateTimeOffset.UtcNow.AddMinutes(-10),
                Confidence = 0.95
            });

            await dbContext.SaveChangesAsync();
        }

        var completeResponse = await client.PostAsync(
            $"/api/v1/work-items/requests/{itemId}/complete",
            content: null);

        Assert.Equal(HttpStatusCode.NoContent, completeResponse.StatusCode);

        var waitingResponse = await client.GetAsync("/api/v1/work-items/waiting");
        var waitingPayload = await waitingResponse.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, waitingResponse.StatusCode);
        Assert.DoesNotContain("Reply to Marina", waitingPayload, StringComparison.Ordinal);

        await using var verificationContext = await CreateDbContextAsync();
        var entity = await verificationContext.WorkItems.SingleAsync(item => item.Id == itemId);
        Assert.NotNull(entity.ResolvedAt);
        Assert.Equal("completed", entity.ResolutionKind);
        Assert.Equal("manual", entity.ResolutionSource);
    }

    [Fact]
    public async Task WorkItemEndpoints_RejectInvalidWorkItemId_WithNotFound()
    {
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await GetAccessTokenAsync(client));

        var response = await client.PostAsync("/api/v1/work-items/requests/not-a-work-item-id/complete", content: null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task WorkItemEndpoints_Dismiss_RemovesMeeting()
    {
        using var client = _factory.CreateClient();
        var accessToken = await GetAccessTokenAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var userId = await GetUserIdAsync("pilot@example.com");
        var sourceEventId = "$evt-api-meeting";
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
                SourceRoom = "!team:matrix.localhost",
                SourceEventId = sourceEventId,
                ObservedAt = scheduledFor.AddHours(-1),
                ScheduledFor = scheduledFor,
                Confidence = 0.84,
                CreatedAt = scheduledFor.AddHours(-1),
                UpdatedAt = scheduledFor.AddHours(-1)
            });

            await dbContext.SaveChangesAsync();
        }

        var dismissResponse = await client.PostAsync(
            $"/api/v1/work-items/events/{meetingId}/dismiss",
            content: null);

        Assert.Equal(HttpStatusCode.NoContent, dismissResponse.StatusCode);

        var meetingsResponse = await client.GetAsync("/api/v1/work-items/meetings");
        var meetingsPayload = await meetingsResponse.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, meetingsResponse.StatusCode);
        Assert.DoesNotContain("Product sync dismiss target", meetingsPayload, StringComparison.Ordinal);

        await using var verificationContext = await CreateDbContextAsync();
        var meeting = await verificationContext.Meetings.SingleAsync(item => item.Id == meetingId);

        Assert.NotNull(meeting.ResolvedAt);
        Assert.Equal("dismissed", meeting.ResolutionKind);
        Assert.Equal("manual", meeting.ResolutionSource);
    }

    [Fact]
    public async Task WorkItemEndpoints_Complete_ActionItemRoute_ResolvesTask()
    {
        using var client = _factory.CreateClient();
        var accessToken = await GetAccessTokenAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var userId = await GetUserIdAsync("pilot@example.com");
        var itemId = Guid.NewGuid();

        await using (var dbContext = await CreateDbContextAsync())
        {
            dbContext.WorkItems.Add(new WorkItemEntity
            {
                Id = itemId,
                UserId = userId,
                Kind = ExtractedItemKind.Task,
                Title = "Prepare deck",
                Summary = "Need to send the deck today.",
                SourceRoom = "!sales:matrix.localhost",
                SourceEventId = "$evt-api-task",
                ObservedAt = DateTimeOffset.UtcNow.AddMinutes(-15),
                Confidence = 0.89
            });

            await dbContext.SaveChangesAsync();
        }

        var completeResponse = await client.PostAsync(
            $"/api/v1/work-items/action-items/{itemId}/complete",
            content: null);

        Assert.Equal(HttpStatusCode.NoContent, completeResponse.StatusCode);

        await using var verificationContext = await CreateDbContextAsync();
        var entity = await verificationContext.WorkItems.SingleAsync(item => item.Id == itemId);
        Assert.NotNull(entity.ResolvedAt);
        Assert.Equal("completed", entity.ResolutionKind);
        Assert.Equal("manual", entity.ResolutionSource);
    }

    [Fact]
    public async Task WorkItemEndpoints_List_And_Search_ReturnCommonCatalog()
    {
        using var client = _factory.CreateClient();
        var accessToken = await GetAccessTokenAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var userId = await GetUserIdAsync("pilot@example.com");
        var waitingId = Guid.NewGuid();
        var taskId = Guid.NewGuid();
        var meetingId = Guid.NewGuid();
        var scheduledFor = DateTimeOffset.UtcNow.AddHours(3);

        await using (var dbContext = await CreateDbContextAsync())
        {
            dbContext.WorkItems.AddRange(
            [
                new WorkItemEntity
                {
                    Id = waitingId,
                    UserId = userId,
                    Kind = ExtractedItemKind.WaitingOn,
                    Title = "Reply to Marina",
                    Summary = "Need to answer Marina today.",
                    SourceRoom = "!sales:matrix.localhost",
                    SourceEventId = "$evt-api-common-waiting",
                    ObservedAt = DateTimeOffset.UtcNow.AddMinutes(-20),
                    Confidence = 0.95
                },
                new WorkItemEntity
                {
                    Id = taskId,
                    UserId = userId,
                    Kind = ExtractedItemKind.Task,
                    Title = "Prepare deck",
                    Summary = "Need to update the sales deck.",
                    SourceRoom = "!sales:matrix.localhost",
                    SourceEventId = "$evt-api-common-task",
                    ObservedAt = DateTimeOffset.UtcNow.AddMinutes(-10),
                    DueAt = scheduledFor,
                    Confidence = 0.87
                }
            ]);

            dbContext.Meetings.Add(new MeetingEntity
            {
                Id = meetingId,
                UserId = userId,
                Title = "Catalog planning sync",
                Summary = "Meet product team in three hours.",
                SourceRoom = "!team:matrix.localhost",
                SourceEventId = "$evt-api-common-meeting",
                ObservedAt = DateTimeOffset.UtcNow.AddMinutes(-5),
                ScheduledFor = scheduledFor,
                Confidence = 0.84,
                CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-5),
                UpdatedAt = DateTimeOffset.UtcNow.AddMinutes(-5)
            });

            await dbContext.SaveChangesAsync();
        }

        var listResponse = await client.GetAsync("/api/v1/work-items");
        var listPayload = await listResponse.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        Assert.Contains("Reply to Marina", listPayload, StringComparison.Ordinal);
        Assert.Contains("Prepare deck", listPayload, StringComparison.Ordinal);
        Assert.Contains("Catalog planning sync", listPayload, StringComparison.Ordinal);

        var requestOnlyResponse = await client.GetAsync($"/api/v1/work-items?type={WorkItemType.Request}");
        var requestOnlyPayload = await requestOnlyResponse.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, requestOnlyResponse.StatusCode);
        Assert.Contains("Reply to Marina", requestOnlyPayload, StringComparison.Ordinal);
        Assert.DoesNotContain("Prepare deck", requestOnlyPayload, StringComparison.Ordinal);
        Assert.DoesNotContain("Catalog planning sync", requestOnlyPayload, StringComparison.Ordinal);

        var searchResponse = await client.GetAsync("/api/v1/work-items/search?q=deck");
        var searchPayload = await searchResponse.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, searchResponse.StatusCode);
        Assert.Contains("Prepare deck", searchPayload, StringComparison.Ordinal);
        Assert.DoesNotContain("Reply to Marina", searchPayload, StringComparison.Ordinal);
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
        catch
        {
        }
    }
}
