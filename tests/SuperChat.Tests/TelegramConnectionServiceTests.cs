using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SuperChat.Contracts.Features.Auth;
using SuperChat.Contracts.Features.Integrations.Matrix;
using SuperChat.Contracts.Features.Integrations.Telegram;
using SuperChat.Domain.Features.Auth;
using SuperChat.Domain.Features.Integrations.Matrix;
using SuperChat.Domain.Features.Integrations.Telegram;
using SuperChat.Infrastructure.Features.Integrations.Telegram;
using SuperChat.Infrastructure.Shared.Persistence;
using MatrixApiClient = SuperChat.Infrastructure.Features.Integrations.Matrix.MatrixApiClient;

namespace SuperChat.Tests;

public sealed class TelegramConnectionServiceTests
{
    [Fact]
    public async Task StartAsync_DevMode_StartsChatLoginWithoutBridgeUrl()
    {
        var user = CreateUser();
        var factory = await CreateFactoryAsync(CancellationToken.None);
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var service = CreateService(factory, handler, CreateIdentity(user.Id), devSeedSampleData: true);

        var connection = await service.StartAsync(user, CancellationToken.None);

        Assert.Equal(TelegramConnectionState.LoginAwaitingPhone, connection.State);
        Assert.Null(connection.WebLoginUrl);

        await using var dbContext = await factory.CreateDbContextAsync(CancellationToken.None);
        var entity = await dbContext.TelegramConnections.SingleAsync(item => item.UserId == user.Id, CancellationToken.None);
        Assert.Equal(TelegramConnectionState.LoginAwaitingPhone, entity.State);
        Assert.Null(entity.WebLoginUrl);
    }

    [Fact]
    public async Task SubmitLoginInputAsync_DevMode_AdvancesChatLoginInsideService()
    {
        var user = CreateUser();
        var factory = await CreateFactoryAsync(CancellationToken.None);
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var service = CreateService(factory, handler, CreateIdentity(user.Id), devSeedSampleData: true);

        var phoneStep = await service.StartAsync(user, CancellationToken.None);
        var codeStep = await service.SubmitLoginInputAsync(user, "+7 (913) 640-78-94", CancellationToken.None);
        var connected = await service.SubmitLoginInputAsync(user, "12345", CancellationToken.None);

        Assert.Equal(TelegramConnectionState.LoginAwaitingPhone, phoneStep.State);
        Assert.Equal(TelegramConnectionState.LoginAwaitingCode, codeStep.State);
        Assert.Equal(TelegramConnectionState.Connected, connected.State);
        Assert.Null(codeStep.WebLoginUrl);
        Assert.Null(connected.WebLoginUrl);
    }

    [Fact]
    public async Task StartAsync_ConnectedSession_SendsLogoutThenLogin()
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

        var handler = new RecordingHandler(request =>
        {
            if (request.RequestUri!.AbsolutePath.Contains("/state/m.room.member/", StringComparison.Ordinal))
            {
                return CreateJsonResponse("""{ "membership": "join" }""");
            }

            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        var service = CreateService(factory, handler, CreateIdentity(user.Id));

        var connection = await service.StartAsync(user, CancellationToken.None);

        Assert.Equal(TelegramConnectionState.LoginAwaitingPhone, connection.State);
        Assert.Equal(4, handler.Requests.Count);
        Assert.Contains("\"body\":\"logout\"", handler.RequestBodies[0], StringComparison.Ordinal);
        Assert.Contains("/state/m.room.member/", handler.Requests[1].RequestUri!.AbsolutePath, StringComparison.Ordinal); // validate existing room
        Assert.Contains("/state/m.room.member/", handler.Requests[2].RequestUri!.AbsolutePath, StringComparison.Ordinal); // wait for bot join
        Assert.Contains("\"body\":\"login\"", handler.RequestBodies[3], StringComparison.Ordinal);
    }

    [Fact]
    public async Task StartAsync_TransientErrorValidatingRoom_KeepsExistingManagementRoom()
    {
        var user = CreateUser();
        var factory = await CreateFactoryAsync(CancellationToken.None);
        await SeedConnectionAsync(
            factory,
            user.Id,
            TelegramConnectionState.BridgePending,
            "!existing:matrix.localhost",
            webLoginUrl: null,
            CancellationToken.None);

        var validationCallCount = 0;
        var handler = new RecordingHandler(request =>
        {
            if (request.RequestUri!.AbsolutePath.Contains("/state/m.room.member/", StringComparison.Ordinal))
            {
                validationCallCount++;
                if (validationCallCount == 1)
                {
                    // First call is management room validation — simulate 500 transient error
                    return new HttpResponseMessage(HttpStatusCode.InternalServerError);
                }

                // Subsequent calls are WaitForBridgeBotJoin — bot joined
                return CreateJsonResponse("""{ "membership": "join" }""");
            }

            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        var service = CreateService(factory, handler, CreateIdentity(user.Id));

        var connection = await service.StartAsync(user, CancellationToken.None);

        // Login should proceed normally
        Assert.Equal(TelegramConnectionState.LoginAwaitingPhone, connection.State);

        // Verify the existing management room was kept (not recreated)
        await using var dbContext = await factory.CreateDbContextAsync(CancellationToken.None);
        var entity = await dbContext.TelegramConnections.SingleAsync(item => item.UserId == user.Id, CancellationToken.None);
        Assert.Equal("!existing:matrix.localhost", entity.ManagementRoomId);

        // No createRoom call should have been made
        Assert.DoesNotContain(handler.Requests, r =>
            r.RequestUri!.AbsolutePath.Contains("/createRoom", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ReconnectAsync_ConnectedSession_SendsLogoutThenLogin()
    {
        var user = CreateUser();
        var factory = await CreateFactoryAsync(CancellationToken.None);
        await SeedIdentityAsync(factory, CreateIdentity(user.Id), CancellationToken.None);
        await SeedConnectionAsync(
            factory,
            user.Id,
            TelegramConnectionState.Connected,
            "!existing:matrix.localhost",
            webLoginUrl: null,
            CancellationToken.None);

        var handler = new RecordingHandler(request =>
        {
            if (request.RequestUri!.AbsolutePath.Contains("/state/m.room.member/", StringComparison.Ordinal))
            {
                return CreateJsonResponse("""
                    {
                      "membership": "join"
                    }
                    """);
            }

            if (request.RequestUri.AbsolutePath.Contains("/send/m.room.message/", StringComparison.Ordinal))
            {
                return new HttpResponseMessage(HttpStatusCode.OK);
            }

            throw new InvalidOperationException($"Unexpected request path: {request.RequestUri.AbsolutePath}");
        });
        var service = CreateService(factory, handler, CreateIdentity(user.Id));

        var connection = await service.ReconnectAsync(user, CancellationToken.None);

        Assert.Equal(TelegramConnectionState.LoginAwaitingPhone, connection.State);
        // DisconnectAsync sends logout, then StartAsync sends login
        Assert.Contains("\"body\":\"logout\"", handler.RequestBodies[0], StringComparison.Ordinal);
        Assert.Contains("\"body\":\"login\"", handler.RequestBodies[^1], StringComparison.Ordinal);

        await using var dbContext = await factory.CreateDbContextAsync(CancellationToken.None);
        var entity = await dbContext.TelegramConnections.SingleAsync(item => item.UserId == user.Id, CancellationToken.None);
        Assert.Equal(TelegramConnectionState.LoginAwaitingPhone, entity.State);
        Assert.Null(entity.WebLoginUrl);
        Assert.Equal("!existing:matrix.localhost", entity.ManagementRoomId);
    }

    [Fact]
    public async Task StartAsync_WaitsForBridgeBotJoinBeforeSendingLogin()
    {
        var user = CreateUser();
        var factory = await CreateFactoryAsync(CancellationToken.None);
        var membershipChecks = 0;
        var handler = new RecordingHandler(request =>
        {
            var path = request.RequestUri!.AbsolutePath;
            if (string.Equals(path, "/_matrix/client/v3/createRoom", StringComparison.Ordinal))
            {
                return CreateJsonResponse("""
                    {
                      "room_id": "!bridge:matrix.localhost"
                    }
                    """);
            }

            if (path.Contains("/state/m.room.member/", StringComparison.Ordinal))
            {
                membershipChecks++;
                return membershipChecks == 1
                    ? new HttpResponseMessage(HttpStatusCode.NotFound)
                    : CreateJsonResponse("""
                        {
                          "membership": "join"
                        }
                        """);
            }

            if (path.Contains("/send/m.room.message/", StringComparison.Ordinal))
            {
                return new HttpResponseMessage(HttpStatusCode.OK);
            }

            throw new InvalidOperationException($"Unexpected request path: {path}");
        });

        var service = CreateService(factory, handler, CreateIdentity(user.Id));

        var connection = await service.StartAsync(user, CancellationToken.None);

        Assert.Equal(TelegramConnectionState.LoginAwaitingPhone, connection.State);
        Assert.Equal(4, handler.Requests.Count);
        Assert.Equal("/_matrix/client/v3/createRoom", handler.Requests[0].RequestUri!.AbsolutePath);
        Assert.Contains("/state/m.room.member/", handler.Requests[1].RequestUri!.AbsolutePath, StringComparison.Ordinal);
        Assert.Contains("/state/m.room.member/", handler.Requests[2].RequestUri!.AbsolutePath, StringComparison.Ordinal);
        Assert.Contains("/send/m.room.message/", handler.Requests[3].RequestUri!.AbsolutePath, StringComparison.Ordinal);

        await using var dbContext = await factory.CreateDbContextAsync(CancellationToken.None);
        var entity = await dbContext.TelegramConnections.SingleAsync(item => item.UserId == user.Id, CancellationToken.None);
        Assert.Equal("!bridge:matrix.localhost", entity.ManagementRoomId);
        Assert.Equal(TelegramConnectionState.LoginAwaitingPhone, entity.State);
    }

    [Fact]
    public async Task GetStatusAsync_ClearsExpiredLoginUrlAndReissuesLogin()
    {
        var now = new DateTimeOffset(2026, 03, 17, 8, 35, 0, TimeSpan.Zero);
        var user = CreateUser();
        var factory = await CreateFactoryAsync(CancellationToken.None);
        await SeedIdentityAsync(factory, CreateIdentity(user.Id), CancellationToken.None);
        await SeedConnectionAsync(
            factory,
            user.Id,
            TelegramConnectionState.BridgePending,
            "!existing:matrix.localhost",
            CreateBridgeLoginUrl(now.AddMinutes(-1)),
            CancellationToken.None);

        var handler = new RecordingHandler(request =>
        {
            if (request.RequestUri!.AbsolutePath.Contains("/state/m.room.member/", StringComparison.Ordinal))
            {
                return CreateJsonResponse("""
                    {
                      "membership": "join"
                    }
                    """);
            }

            if (request.RequestUri.AbsolutePath.Contains("/send/m.room.message/", StringComparison.Ordinal))
            {
                return new HttpResponseMessage(HttpStatusCode.OK);
            }

            throw new InvalidOperationException($"Unexpected request path: {request.RequestUri.AbsolutePath}");
        });

        var service = CreateService(factory, handler, CreateIdentity(user.Id), new FixedTimeProvider(now));

        var connection = await service.GetStatusAsync(user.Id, CancellationToken.None);

        Assert.Equal(TelegramConnectionState.BridgePending, connection.State);
        Assert.Null(connection.WebLoginUrl);
        Assert.Equal(2, handler.Requests.Count);
        Assert.Equal(HttpMethod.Get, handler.Requests[0].Method);
        Assert.Contains("/state/m.room.member/", handler.Requests[0].RequestUri!.AbsolutePath, StringComparison.Ordinal);
        Assert.Equal(HttpMethod.Put, handler.Requests[1].Method);
        Assert.Contains("/send/m.room.message/", handler.Requests[1].RequestUri!.AbsolutePath, StringComparison.Ordinal);

        await using var dbContext = await factory.CreateDbContextAsync(CancellationToken.None);
        var entity = await dbContext.TelegramConnections.SingleAsync(item => item.UserId == user.Id, CancellationToken.None);
        Assert.Null(entity.WebLoginUrl);
    }

    [Fact]
    public async Task GetStatusAsync_LeavesFreshLoginUrlUntouched()
    {
        var now = new DateTimeOffset(2026, 03, 17, 8, 35, 0, TimeSpan.Zero);
        var user = CreateUser();
        var factory = await CreateFactoryAsync(CancellationToken.None);
        await SeedIdentityAsync(factory, CreateIdentity(user.Id), CancellationToken.None);
        var freshUrl = CreateBridgeLoginUrl(now.AddMinutes(5));
        await SeedConnectionAsync(
            factory,
            user.Id,
            TelegramConnectionState.BridgePending,
            "!existing:matrix.localhost",
            freshUrl,
            CancellationToken.None);

        var handler = new RecordingHandler(_ => throw new InvalidOperationException("No Matrix calls expected for a fresh login URL."));
        var service = CreateService(factory, handler, CreateIdentity(user.Id), new FixedTimeProvider(now));

        var connection = await service.GetStatusAsync(user.Id, CancellationToken.None);

        Assert.Equal(new Uri(freshUrl), connection.WebLoginUrl);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public void TryGetBridgeLoginExpiry_ReturnsEmbeddedExpiry()
    {
        var expected = new DateTimeOffset(2026, 03, 17, 8, 22, 7, TimeSpan.Zero);
        var url = CreateBridgeLoginUrl(expected);

        var result = TelegramConnectionService.TryGetBridgeLoginExpiry(url);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("+7 (913) 640-78-94", "+79136407894")]
    [InlineData("+7 999 123-45-67", "+79991234567")]
    [InlineData("+79136407894", "+79136407894")]
    [InlineData("89136407894", "89136407894")]
    [InlineData("+1 (555) 123 4567", "+15551234567")]
    [InlineData("  +7 913 640 78 94  ", "+79136407894")]
    public void NormalizePhoneNumber_StripsFormattingCharacters(string input, string expected)
    {
        var result = TelegramConnectionService.NormalizePhoneNumber(input);

        Assert.Equal(expected, result);
    }

    [Fact]
    public async Task SubmitLoginInputAsync_NormalizesPhoneAndResendLogin()
    {
        var user = CreateUser();
        var factory = await CreateFactoryAsync(CancellationToken.None);
        await SeedConnectionAsync(
            factory, user.Id, TelegramConnectionState.LoginAwaitingPhone,
            "!mgmt:matrix.localhost", webLoginUrl: null, CancellationToken.None);

        var handler = new RecordingHandler(CreateBridgeReadyHandler());
        var service = CreateService(factory, handler, CreateIdentity(user.Id));

        await service.SubmitLoginInputAsync(user, "+7 (913) 640-78-94", CancellationToken.None);

        Assert.Contains("\"body\":\"login\"", handler.RequestBodies[^2], StringComparison.Ordinal);
        Assert.Contains("79136407894", handler.RequestBodies[^1], StringComparison.Ordinal);
        Assert.DoesNotContain("(", handler.RequestBodies[^1], StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(TelegramConnectionState.LoginAwaitingCode, "12345")]
    [InlineData(TelegramConnectionState.LoginAwaitingPassword, "my2faPassword")]
    public async Task SubmitLoginInputAsync_DoesNotResendLogin_ForNonPhoneStep(
        TelegramConnectionState state, string input)
    {
        var user = CreateUser();
        var factory = await CreateFactoryAsync(CancellationToken.None);
        await SeedConnectionAsync(factory, user.Id, state, "!mgmt:matrix.localhost", webLoginUrl: null, CancellationToken.None);

        var handler = new RecordingHandler(CreateBridgeReadyHandler());
        var service = CreateService(factory, handler, CreateIdentity(user.Id));

        await service.SubmitLoginInputAsync(user, input, CancellationToken.None);

        // Code/password steps: membership check + send input (no login re-issue)
        var sendRequests = handler.RequestBodies
            .OfType<string>()
            .Where(static body => body.Contains("\"body\"", StringComparison.Ordinal))
            .ToList();
        var lastSendRequest = sendRequests[^1];
        Assert.Contains($"\"body\":\"{input}\"", lastSendRequest, StringComparison.Ordinal);
        Assert.DoesNotContain(lastSendRequest, "\"body\":\"login\"");
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("")]
    [InlineData("---")]
    public void NormalizePhoneNumber_ReturnsEmpty_WhenNoDigits(string input)
    {
        var result = TelegramConnectionService.NormalizePhoneNumber(input);

        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public async Task SubmitLoginInputAsync_RejectsInvalidPhone_WhenNoDigits()
    {
        var user = CreateUser();
        var factory = await CreateFactoryAsync(CancellationToken.None);
        await SeedConnectionAsync(
            factory, user.Id, TelegramConnectionState.LoginAwaitingPhone,
            "!mgmt:matrix.localhost", webLoginUrl: null, CancellationToken.None);

        var handler = new RecordingHandler(CreateBridgeReadyHandler());
        var service = CreateService(factory, handler, CreateIdentity(user.Id));

        await service.SubmitLoginInputAsync(user, "abc", CancellationToken.None);

        // Membership check happens, but no message sent to bridge
        var sendBodies = handler.RequestBodies.Where(b => b != null && b.Contains("\"body\"")).ToList();
        Assert.Empty(sendBodies);
    }

    [Fact]
    public async Task SubmitLoginInputAsync_AutoStartsLogin_WhenNotInLoginFlow()
    {
        var user = CreateUser();
        var factory = await CreateFactoryAsync(CancellationToken.None);
        await SeedConnectionAsync(
            factory, user.Id, TelegramConnectionState.Disconnected,
            "!mgmt:matrix.localhost", webLoginUrl: null, CancellationToken.None);

        var handler = new RecordingHandler(CreateBridgeReadyHandler());
        var service = CreateService(factory, handler, CreateIdentity(user.Id));

        await service.SubmitLoginInputAsync(user, "+79136407894", CancellationToken.None);

        Assert.Contains("79136407894", handler.RequestBodies[^1], StringComparison.Ordinal);
    }

    [Fact]
    public async Task SubmitLoginInputAsync_WhenLoginFails_StillSendsPhone()
    {
        var user = CreateUser();
        var factory = await CreateFactoryAsync(CancellationToken.None);
        await SeedConnectionAsync(
            factory, user.Id, TelegramConnectionState.LoginAwaitingPhone,
            "!mgmt:matrix.localhost", webLoginUrl: null, CancellationToken.None);

        var sendCount = 0;
        var handler = new RecordingHandler(request =>
        {
            if (request.RequestUri!.AbsolutePath.Contains("/state/m.room.member/", StringComparison.Ordinal))
            {
                return CreateJsonResponse("""{ "membership": "join" }""");
            }

            sendCount++;
            return sendCount == 1
                ? throw new HttpRequestException("Simulated failure")
                : new HttpResponseMessage(HttpStatusCode.OK);
        });
        var service = CreateService(factory, handler, CreateIdentity(user.Id));

        await service.SubmitLoginInputAsync(user, "+79136407894", CancellationToken.None);

        Assert.Contains("79136407894", handler.RequestBodies[^1], StringComparison.Ordinal);
    }

    [Fact]
    public async Task SubmitLoginInputAsync_WhenSendFails_SetsErrorState()
    {
        var user = CreateUser();
        var factory = await CreateFactoryAsync(CancellationToken.None);
        await SeedConnectionAsync(
            factory, user.Id, TelegramConnectionState.LoginAwaitingCode,
            "!mgmt:matrix.localhost", webLoginUrl: null, CancellationToken.None);

        var handler = new RecordingHandler(request =>
        {
            if (request.RequestUri!.AbsolutePath.Contains("/state/m.room.member/", StringComparison.Ordinal))
            {
                return CreateJsonResponse("""{ "membership": "join" }""");
            }

            throw new HttpRequestException("Simulated failure");
        });
        var service = CreateService(factory, handler, CreateIdentity(user.Id));

        await service.SubmitLoginInputAsync(user, "12345", CancellationToken.None);

        await using var dbContext = await factory.CreateDbContextAsync(CancellationToken.None);
        var entity = await dbContext.TelegramConnections.SingleAsync(item => item.UserId == user.Id, CancellationToken.None);
        Assert.Equal(TelegramConnectionState.Error, entity.State);
    }

    private static Func<HttpRequestMessage, HttpResponseMessage> CreateBridgeReadyHandler()
    {
        return request =>
        {
            if (request.RequestUri!.AbsolutePath.Contains("/state/m.room.member/", StringComparison.Ordinal))
            {
                return CreateJsonResponse("""{ "membership": "join" }""");
            }

            return new HttpResponseMessage(HttpStatusCode.OK);
        };
    }

    private static TelegramConnectionService CreateService(
        IDbContextFactory<SuperChatDbContext> factory,
        RecordingHandler handler,
        MatrixIdentity identity,
        TimeProvider? timeProvider = null,
        bool devSeedSampleData = false)
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
            Options.Create(new TelegramBridgeOptions
            {
                BotUserId = "@telegrambot:matrix.localhost"
            }),
            Options.Create(new PilotOptions
            {
                DevSeedSampleData = devSeedSampleData
            }),
            timeProvider ?? TimeProvider.System,
            NullLogger<TelegramConnectionService>.Instance);
    }

    private static AppUser CreateUser()
    {
        var now = DateTimeOffset.UtcNow;
        return new AppUser(Guid.NewGuid(), new Email("pilot@example.com"), now, now);
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

    private static async Task SeedIdentityAsync(
        IDbContextFactory<SuperChatDbContext> factory,
        MatrixIdentity identity,
        CancellationToken cancellationToken)
    {
        await using var dbContext = await factory.CreateDbContextAsync(cancellationToken);
        dbContext.MatrixIdentities.Add(new MatrixIdentityEntity
        {
            UserId = identity.UserId,
            MatrixUserId = identity.MatrixUserId,
            AccessToken = identity.AccessToken,
            ProvisionedAt = identity.ProvisionedAt
        });

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static string CreateBridgeLoginUrl(DateTimeOffset expiry)
    {
        var payload = $"{{\"mxid\":\"@pilot:matrix.localhost\",\"endpoint\":\"/login\",\"expiry\":{expiry.ToUnixTimeSeconds()}}}";
        var encodedPayload = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(payload))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

        return $"https://bridge.localhost/public/login?token=random-prefix:{encodedPayload}";
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

    private static HttpResponseMessage CreateJsonResponse(string content)
    {
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(content)
        };
    }

    private sealed class RecordingHandler(Func<HttpRequestMessage, HttpResponseMessage> responseFactory) : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = [];
        public List<string?> RequestBodies { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            RequestBodies.Add(request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken));
            return responseFactory(request);
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

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow()
        {
            return utcNow;
        }
    }
}
