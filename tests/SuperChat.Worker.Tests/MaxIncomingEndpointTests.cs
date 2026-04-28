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
using SuperChat.Infrastructure.Shared.Persistence;
using SuperChat.Worker.Features.Integrations.Max.Internal;

namespace SuperChat.Worker.Tests;

public sealed class MaxIncomingEndpointTests : IClassFixture<MaxIncomingWorkerTestFactory>
{
    private const string HmacSecret = "test-max-secret-987";

    private readonly MaxIncomingWorkerTestFactory _factory;

    public MaxIncomingEndpointTests(MaxIncomingWorkerTestFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Incoming_StoresMessageWithMaxSource_WhenSignatureIsValid()
    {
        using var client = _factory.CreateClient();
        var userId = Guid.NewGuid();
        var externalMessageId = $"max:msg:{Guid.NewGuid():N}";

        var body = JsonSerializer.Serialize(new
        {
            user_id = userId,
            external_chat_id = "max:chat:99",
            external_message_id = externalMessageId,
            sender_name = "Carol",
            text = "Привет из Max",
            sent_at = new DateTimeOffset(2026, 04, 19, 12, 0, 0, TimeSpan.Zero)
        });

        using var request = BuildSignedRequest(body, HmacSecret);
        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        await using var dbContext = await CreateDbContextAsync();
        var stored = await dbContext.ChatMessages
            .SingleAsync(item => item.ExternalMessageId == externalMessageId);

        Assert.Equal("max", stored.Source);
        Assert.Equal(userId, stored.UserId);
        Assert.Equal("Carol", stored.SenderName);
    }

    [Fact]
    public async Task Incoming_ReturnsUnauthorized_WhenSignatureIsInvalid()
    {
        using var client = _factory.CreateClient();
        var body = JsonSerializer.Serialize(new
        {
            user_id = Guid.NewGuid(),
            external_chat_id = "max:chat:99",
            external_message_id = "max:msg:bad",
            sender_name = "Carol",
            text = "hello",
            sent_at = DateTimeOffset.UtcNow
        });

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/internal/max/incoming")
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
        request.Headers.Add("X-Superchat-Signature", "sha256=deadbeef");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private static HttpRequestMessage BuildSignedRequest(string body, string secret)
    {
        var signature = MaxIncomingEndpoint.ComputeSignature(body, secret);
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/internal/max/incoming")
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

public sealed class MaxIncomingWorkerTestFactory : WebApplicationFactory<SuperChat.Worker.Program>
{
    static MaxIncomingWorkerTestFactory()
    {
        Environment.SetEnvironmentVariable("Persistence__Provider", "Sqlite");
        Environment.SetEnvironmentVariable("PipelineMessaging__Enabled", "false");
    }

    private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"superchat-worker-max-{Guid.NewGuid():N}.db");

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
                ["MaxUserbot:Enabled"] = "true",
                ["MaxUserbot:HmacSecret"] = "test-max-secret-987"
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
