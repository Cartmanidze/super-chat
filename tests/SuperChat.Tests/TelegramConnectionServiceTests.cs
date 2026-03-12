using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SuperChat.Contracts.Configuration;
using SuperChat.Domain.Model;
using SuperChat.Infrastructure.Abstractions;
using SuperChat.Infrastructure.Persistence;
using SuperChat.Infrastructure.Services;

namespace SuperChat.Tests;

public sealed class TelegramConnectionServiceTests
{
    [Fact]
    public async Task StartAsync_ReusesConnectedSession_WhenNoLoginUrlIsStored()
    {
        var user = CreateUser();
        var factory = await CreateFactoryAsync(CancellationToken.None);
        await SeedConnectionAsync(
            factory,
            user.Id,
            TelegramConnectionState.Connected,
            "!existing:matrix.localhost",
            webLoginUrl: null,
            CancellationToken.None);

        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var service = CreateService(factory, handler, CreateIdentity(user.Id));

        var connection = await service.StartAsync(user, CancellationToken.None);

        Assert.Equal(TelegramConnectionState.Connected, connection.State);
        Assert.Null(connection.WebLoginUrl);
        Assert.Equal(0, handler.RequestCount);
    }

    [Fact]
    public async Task StartAsync_RegeneratesLogin_WhenConnectedSessionStillHasLoginUrl()
    {
        var user = CreateUser();
        var factory = await CreateFactoryAsync(CancellationToken.None);
        await SeedConnectionAsync(
            factory,
            user.Id,
            TelegramConnectionState.Connected,
            "!existing:matrix.localhost",
            "https://bridge.localhost/public/login?token=expired",
            CancellationToken.None);

        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var service = CreateService(factory, handler, CreateIdentity(user.Id));

        var connection = await service.StartAsync(user, CancellationToken.None);

        Assert.Equal(TelegramConnectionState.BridgePending, connection.State);
        Assert.Null(connection.WebLoginUrl);
        Assert.Equal(1, handler.RequestCount);
        Assert.NotNull(handler.LastRequest);
        Assert.Equal(HttpMethod.Put, handler.LastRequest!.Method);
        Assert.Contains("/send/m.room.message/", handler.LastRequest.RequestUri!.AbsolutePath, StringComparison.Ordinal);

        await using var dbContext = await factory.CreateDbContextAsync(CancellationToken.None);
        var entity = await dbContext.TelegramConnections.SingleAsync(item => item.UserId == user.Id, CancellationToken.None);
        Assert.Equal(TelegramConnectionState.BridgePending, entity.State);
        Assert.Null(entity.WebLoginUrl);
        Assert.Equal("!existing:matrix.localhost", entity.ManagementRoomId);
    }

    private static TelegramConnectionService CreateService(
        IDbContextFactory<SuperChatDbContext> factory,
        RecordingHandler handler,
        MatrixIdentity identity)
    {
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://matrix.localhost")
        };

        var matrixApiClient = new MatrixApiClient(
            httpClient,
            Options.Create(new MatrixOptions()),
            NullLogger<MatrixApiClient>.Instance);

        return new TelegramConnectionService(
            factory,
            new FakeMatrixProvisioningService(identity),
            matrixApiClient,
            Options.Create(new TelegramBridgeOptions()),
            Options.Create(new PilotOptions
            {
                DevSeedSampleData = false
            }),
            TimeProvider.System,
            NullLogger<TelegramConnectionService>.Instance);
    }

    private static AppUser CreateUser()
    {
        var now = DateTimeOffset.UtcNow;
        return new AppUser(Guid.NewGuid(), "pilot@example.com", now, now);
    }

    private static MatrixIdentity CreateIdentity(Guid userId)
    {
        return new MatrixIdentity(userId, "@pilot:matrix.localhost", "live-access-token", DateTimeOffset.UtcNow);
    }

    private static async Task<IDbContextFactory<SuperChatDbContext>> CreateFactoryAsync(CancellationToken cancellationToken)
    {
        var dbContextOptions = new DbContextOptionsBuilder<SuperChatDbContext>()
            .UseInMemoryDatabase($"superchat-telegram-{Guid.NewGuid():N}")
            .Options;

        var factory = new TestDbContextFactory(dbContextOptions);
        await using var dbContext = await factory.CreateDbContextAsync(cancellationToken);
        await dbContext.Database.EnsureCreatedAsync(cancellationToken);
        return factory;
    }

    private static async Task SeedConnectionAsync(
        IDbContextFactory<SuperChatDbContext> factory,
        Guid userId,
        TelegramConnectionState state,
        string managementRoomId,
        string? webLoginUrl,
        CancellationToken cancellationToken)
    {
        await using var dbContext = await factory.CreateDbContextAsync(cancellationToken);
        dbContext.TelegramConnections.Add(new TelegramConnectionEntity
        {
            UserId = userId,
            State = state,
            ManagementRoomId = managementRoomId,
            WebLoginUrl = webLoginUrl,
            UpdatedAt = DateTimeOffset.UtcNow
        });

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private sealed class FakeMatrixProvisioningService(MatrixIdentity identity) : IMatrixProvisioningService
    {
        public Task<MatrixIdentity> EnsureIdentityAsync(AppUser user, CancellationToken cancellationToken)
        {
            return Task.FromResult(identity);
        }

        public Task<MatrixIdentity?> GetIdentityAsync(Guid userId, CancellationToken cancellationToken)
        {
            return Task.FromResult<MatrixIdentity?>(userId == identity.UserId ? identity : null);
        }
    }

    private sealed class RecordingHandler(Func<HttpRequestMessage, HttpResponseMessage> responseFactory) : HttpMessageHandler
    {
        public int RequestCount { get; private set; }

        public HttpRequestMessage? LastRequest { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCount++;
            LastRequest = request;
            return Task.FromResult(responseFactory(request));
        }
    }

    private sealed class TestDbContextFactory(DbContextOptions<SuperChatDbContext> options) : IDbContextFactory<SuperChatDbContext>
    {
        public SuperChatDbContext CreateDbContext()
        {
            return new SuperChatDbContext(options);
        }

        public Task<SuperChatDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new SuperChatDbContext(options));
        }
    }
}
