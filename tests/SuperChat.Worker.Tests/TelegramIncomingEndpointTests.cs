using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using SuperChat.Domain.Features.Integrations.Telegram;
using SuperChat.Infrastructure.Shared.Persistence;
using SuperChat.Worker.Features.Integrations.Telegram.Internal;

namespace SuperChat.Worker.Tests;

public sealed class TelegramIncomingEndpointTests : IClassFixture<TelegramIncomingWorkerTestFactory>
{
    private const string HmacSecret = "test-hmac-secret-123";

    private readonly TelegramIncomingWorkerTestFactory _factory;

    public TelegramIncomingEndpointTests(TelegramIncomingWorkerTestFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Incoming_StoresMessage_WhenSignatureIsValid()
    {
        using var client = _factory.CreateClient();
        var userId = Guid.NewGuid();
        var externalMessageId = $"tg:msg:{Guid.NewGuid():N}";

        var body = JsonSerializer.Serialize(new
        {
            user_id = userId,
            external_chat_id = "tg:chat:42",
            external_message_id = externalMessageId,
            sender_name = "Alice",
            text = "Привет, ты на связи?",
            sent_at = new DateTimeOffset(2026, 04, 19, 10, 0, 0, TimeSpan.Zero)
        });

        using var request = BuildSignedRequest(body, HmacSecret);
        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        await using var dbContext = await CreateDbContextAsync();
        var stored = await dbContext.ChatMessages
            .SingleAsync(item => item.ExternalMessageId == externalMessageId);

        Assert.Equal(userId, stored.UserId);
        Assert.Equal("telegram", stored.Source);
        Assert.Equal("tg:chat:42", stored.ExternalChatId);
        Assert.Equal("Alice", stored.SenderName);
    }

    [Fact]
    public async Task Incoming_ReturnsUnauthorized_WhenSignatureHeaderIsMissing()
    {
        using var client = _factory.CreateClient();
        var body = JsonSerializer.Serialize(new { user_id = Guid.NewGuid() });
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/internal/telegram/incoming")
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Incoming_ReturnsUnauthorized_WhenSignatureIsInvalid()
    {
        using var client = _factory.CreateClient();
        var body = JsonSerializer.Serialize(new
        {
            user_id = Guid.NewGuid(),
            external_chat_id = "tg:chat:42",
            external_message_id = "tg:msg:bad",
            sender_name = "Alice",
            text = "hello",
            sent_at = DateTimeOffset.UtcNow
        });

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/internal/telegram/incoming")
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
        request.Headers.Add("X-Superchat-Signature", "sha256=deadbeef");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Incoming_ReturnsBadRequest_WhenPayloadIsInvalid()
    {
        using var client = _factory.CreateClient();
        var body = JsonSerializer.Serialize(new
        {
            user_id = Guid.Empty,
            external_chat_id = "",
            external_message_id = "",
            sender_name = "",
            text = "x",
            sent_at = DateTimeOffset.UtcNow
        });

        using var request = BuildSignedRequest(body, HmacSecret);
        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task SessionRevoked_UpdatesConnectionStateToRevoked_WhenSignatureIsValid()
    {
        using var client = _factory.CreateClient();
        var userId = Guid.NewGuid();

        // Сначала переведём пользователя в Connected, чтобы было что отзывать.
        await using (var seedDbContext = await CreateDbContextAsync())
        {
            seedDbContext.TelegramConnections.Add(new TelegramConnectionEntity
            {
                UserId = userId,
                State = TelegramConnectionState.Connected,
                UpdatedAt = DateTimeOffset.UtcNow.AddMinutes(-10)
            });
            await seedDbContext.SaveChangesAsync();
        }

        var body = JsonSerializer.Serialize(new
        {
            user_id = userId,
            reason = "not_authorized_on_resume",
            timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        });

        using var request = BuildSignedRequestForRevoked(body, HmacSecret);
        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        await using var dbContext = await CreateDbContextAsync();
        var connection = await dbContext.TelegramConnections.SingleAsync(item => item.UserId == userId);
        Assert.Equal(TelegramConnectionState.Revoked, connection.State);
    }

    [Fact]
    public async Task SessionRevoked_ReturnsUnauthorized_WhenSignatureIsInvalid()
    {
        using var client = _factory.CreateClient();
        var body = JsonSerializer.Serialize(new
        {
            user_id = Guid.NewGuid(),
            reason = "x",
            timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        });

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/internal/telegram/session-revoked")
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
        request.Headers.Add("X-Superchat-Signature", "sha256=deadbeef");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task SessionRevoked_ReturnsBadRequest_WhenUserIdMissing()
    {
        using var client = _factory.CreateClient();
        var body = JsonSerializer.Serialize(new
        {
            user_id = Guid.Empty,
            reason = "x",
            timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        });

        using var request = BuildSignedRequestForRevoked(body, HmacSecret);
        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task SessionRevoked_ReturnsUnauthorized_WhenTimestampIsStale()
    {
        using var client = _factory.CreateClient();
        var stale = DateTimeOffset.UtcNow.AddHours(-1).ToUnixTimeSeconds();
        var body = JsonSerializer.Serialize(new
        {
            user_id = Guid.NewGuid(),
            reason = "x",
            timestamp = stale
        });

        using var request = BuildSignedRequestForRevoked(body, HmacSecret);
        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private static HttpRequestMessage BuildSignedRequest(string body, string secret)
    {
        var signature = TelegramIncomingEndpoint.ComputeSignature(body, secret);
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/internal/telegram/incoming")
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
        request.Headers.Add("X-Superchat-Signature", $"sha256={signature}");
        return request;
    }

    private static HttpRequestMessage BuildSignedRequestForRevoked(string body, string secret)
    {
        var signature = TelegramIncomingEndpoint.ComputeSignature(body, secret);
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/internal/telegram/session-revoked")
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
        request.Headers.Add("X-Superchat-Signature", $"sha256={signature}");
        return request;
    }

    private async Task<SuperChatDbContext> CreateDbContextAsync()
    {
        var factory = _factory.Services.GetRequiredService<IDbContextFactory<SuperChatDbContext>>();
        return await factory.CreateDbContextAsync();
    }
}

public sealed class TelegramIncomingWorkerTestFactory : WebApplicationFactory<SuperChat.Worker.Program>
{
    static TelegramIncomingWorkerTestFactory()
    {
        Environment.SetEnvironmentVariable("Persistence__Provider", "Sqlite");
        Environment.SetEnvironmentVariable("PipelineMessaging__Enabled", "false");
    }

    private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"superchat-worker-telegram-{Guid.NewGuid():N}.db");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:SuperChatDb"] = $"Data Source={_databasePath}",
                ["Persistence:Provider"] = "Sqlite",
                ["PipelineMessaging:Enabled"] = "false",
                ["SuperChat:DevSeedSampleData"] = "false",
                ["TelegramUserbot:Enabled"] = "true",
                ["TelegramUserbot:HmacSecret"] = "test-hmac-secret-123"
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
