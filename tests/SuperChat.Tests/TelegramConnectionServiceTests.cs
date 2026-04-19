using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SuperChat.Contracts.Features.Auth;
using SuperChat.Domain.Features.Auth;
using SuperChat.Domain.Features.Integrations.Telegram;
using SuperChat.Infrastructure.Features.Integrations.Telegram;
using SuperChat.Infrastructure.Features.Integrations.Telegram.Userbot;
using SuperChat.Infrastructure.Shared.Persistence;

namespace SuperChat.Tests;

public sealed class TelegramConnectionServiceTests
{
    [Fact]
    public async Task StartAsync_TransitionsTo_LoginAwaitingPhone()
    {
        await using var fixture = await TestFixture.CreateAsync();
        var handler = new StubHttpHandler();
        var service = fixture.CreateService(handler);
        var user = CreateUser();

        var connection = await service.StartAsync(user, CancellationToken.None);

        Assert.Equal(TelegramConnectionState.LoginAwaitingPhone, connection.State);
    }

    [Fact]
    public async Task SubmitLoginInputAsync_Phone_CallsClient_AndMovesTo_AwaitingCode()
    {
        await using var fixture = await TestFixture.CreateAsync();
        var handler = new StubHttpHandler
        {
            Connect = (_, _) => JsonResponse(HttpStatusCode.Accepted, new { status = "awaiting_code", phone_code_hash = "hash-1" })
        };
        var service = fixture.CreateService(handler);
        var user = CreateUser();

        await service.StartAsync(user, CancellationToken.None);
        var afterPhone = await service.SubmitLoginInputAsync(user, "+7 (000) 111-22-33", CancellationToken.None);

        Assert.Equal(TelegramConnectionState.LoginAwaitingCode, afterPhone.State);
        var sentConnect = Assert.Single(handler.Requests, r => r.Path.EndsWith("/connect", StringComparison.Ordinal));
        Assert.Contains("70001112233", sentConnect.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SubmitLoginInputAsync_Phone_InvalidInput_KeepsPreviousState()
    {
        await using var fixture = await TestFixture.CreateAsync();
        var handler = new StubHttpHandler();
        var service = fixture.CreateService(handler);
        var user = CreateUser();

        await service.StartAsync(user, CancellationToken.None);
        var afterEmpty = await service.SubmitLoginInputAsync(user, "not-a-phone", CancellationToken.None);

        Assert.Equal(TelegramConnectionState.LoginAwaitingPhone, afterEmpty.State);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task SubmitLoginInputAsync_Code_Connected_MovesTo_Connected()
    {
        await using var fixture = await TestFixture.CreateAsync();
        var handler = new StubHttpHandler
        {
            Connect = (_, _) => JsonResponse(HttpStatusCode.Accepted, new { status = "awaiting_code", phone_code_hash = "hash-1" }),
            Code = (_, _) => JsonResponse(HttpStatusCode.OK, new { status = "connected" })
        };
        var service = fixture.CreateService(handler);
        var user = CreateUser();

        await service.StartAsync(user, CancellationToken.None);
        await service.SubmitLoginInputAsync(user, "+70001112233", CancellationToken.None);
        var afterCode = await service.SubmitLoginInputAsync(user, "12345", CancellationToken.None);

        Assert.Equal(TelegramConnectionState.Connected, afterCode.State);
    }

    [Fact]
    public async Task SubmitLoginInputAsync_Code_202Accepted_MovesTo_LoginAwaitingPassword()
    {
        await using var fixture = await TestFixture.CreateAsync();
        var handler = new StubHttpHandler
        {
            Connect = (_, _) => JsonResponse(HttpStatusCode.Accepted, new { status = "awaiting_code", phone_code_hash = "hash-1" }),
            Code = (_, _) => new HttpResponseMessage(HttpStatusCode.Accepted)
        };
        var service = fixture.CreateService(handler);
        var user = CreateUser();

        await service.StartAsync(user, CancellationToken.None);
        await service.SubmitLoginInputAsync(user, "+70001112233", CancellationToken.None);
        var afterCode = await service.SubmitLoginInputAsync(user, "12345", CancellationToken.None);

        Assert.Equal(TelegramConnectionState.LoginAwaitingPassword, afterCode.State);
    }

    [Fact]
    public async Task DisconnectAsync_CallsClient_AndMarksDisconnected()
    {
        await using var fixture = await TestFixture.CreateAsync();
        var handler = new StubHttpHandler();
        var service = fixture.CreateService(handler);
        var user = CreateUser();

        await service.StartAsync(user, CancellationToken.None);
        await service.DisconnectAsync(user.Id, CancellationToken.None);

        var status = await service.GetStatusAsync(user.Id, CancellationToken.None);
        Assert.Equal(TelegramConnectionState.Disconnected, status.State);
        Assert.Contains(handler.Requests, r => r.Path.EndsWith("/disconnect", StringComparison.Ordinal));
    }

    private static HttpResponseMessage JsonResponse(HttpStatusCode code, object payload)
    {
        return new HttpResponseMessage(code)
        {
            Content = JsonContent.Create(payload)
        };
    }

    private static AppUser CreateUser()
    {
        return new AppUser(
            Guid.NewGuid(),
            new SuperChat.Domain.Features.Auth.Email("pilot@example.com"),
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow,
            null);
    }

    private sealed class StubHttpHandler : HttpMessageHandler
    {
        public List<RecordedRequest> Requests { get; } = [];

        public Func<HttpRequestMessage, CancellationToken, HttpResponseMessage>? Connect { get; set; }
        public Func<HttpRequestMessage, CancellationToken, HttpResponseMessage>? Code { get; set; }
        public Func<HttpRequestMessage, CancellationToken, HttpResponseMessage>? Password { get; set; }
        public Func<HttpRequestMessage, CancellationToken, HttpResponseMessage>? Disconnect { get; set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri?.AbsolutePath ?? string.Empty;
            var body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
            Requests.Add(new RecordedRequest(path, body));

            if (path.EndsWith("/connect", StringComparison.Ordinal))
            {
                return Connect?.Invoke(request, cancellationToken)
                    ?? JsonResponse(HttpStatusCode.Accepted, new { status = "awaiting_code", phone_code_hash = "hash-default" });
            }

            if (path.EndsWith("/code", StringComparison.Ordinal))
            {
                return Code?.Invoke(request, cancellationToken)
                    ?? JsonResponse(HttpStatusCode.OK, new { status = "connected" });
            }

            if (path.EndsWith("/password", StringComparison.Ordinal))
            {
                return Password?.Invoke(request, cancellationToken)
                    ?? JsonResponse(HttpStatusCode.OK, new { status = "connected" });
            }

            if (path.EndsWith("/disconnect", StringComparison.Ordinal))
            {
                return Disconnect?.Invoke(request, cancellationToken)
                    ?? new HttpResponseMessage(HttpStatusCode.OK);
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        }
    }

    private sealed record RecordedRequest(string Path, string Body);

    private sealed class TestFixture : IAsyncDisposable
    {
        public IDbContextFactory<SuperChatDbContext> DbContextFactory { get; }
        public IOptions<PilotOptions> PilotOptions { get; }
        public TimeProvider TimeProvider { get; }

        private TestFixture(IDbContextFactory<SuperChatDbContext> factory, IOptions<PilotOptions> pilotOptions)
        {
            DbContextFactory = factory;
            PilotOptions = pilotOptions;
            TimeProvider = TimeProvider.System;
        }

        public static async Task<TestFixture> CreateAsync()
        {
            var options = new DbContextOptionsBuilder<SuperChatDbContext>()
                .UseInMemoryDatabase($"superchat-userbot-connect-{Guid.NewGuid():N}")
                .Options;

            var factory = new InMemoryDbContextFactory(options);
            await using var dbContext = await factory.CreateDbContextAsync();
            await dbContext.Database.EnsureCreatedAsync();

            var pilotOptions = Options.Create(new PilotOptions { DevSeedSampleData = false });
            return new TestFixture(factory, pilotOptions);
        }

        public TelegramConnectionService CreateService(HttpMessageHandler handler)
        {
            var httpClient = new HttpClient(handler)
            {
                BaseAddress = new Uri("http://telegram-userbot-service:7491/")
            };
            var client = new TelegramUserbotClient(httpClient, NullLogger<TelegramUserbotClient>.Instance);

            return new TelegramConnectionService(
                DbContextFactory,
                client,
                PilotOptions,
                TimeProvider,
                NullLogger<TelegramConnectionService>.Instance);
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class InMemoryDbContextFactory(DbContextOptions<SuperChatDbContext> options) : IDbContextFactory<SuperChatDbContext>
    {
        public SuperChatDbContext CreateDbContext() => new(options);

        public Task<SuperChatDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new SuperChatDbContext(options));
    }
}
