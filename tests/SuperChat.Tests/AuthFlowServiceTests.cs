using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using SuperChat.Contracts.Features.Auth;
using SuperChat.Contracts.Features.Integrations.Matrix;
using SuperChat.Infrastructure.Features.Auth;
using SuperChat.Infrastructure.Features.Integrations.Matrix;
using SuperChat.Infrastructure.Shared.Persistence;

namespace SuperChat.Tests;

public sealed class AuthFlowServiceTests
{
    private readonly FakeTimeProvider _timeProvider = new(DateTimeOffset.UtcNow);

    // --- SendCode tests ---

    [Fact]
    public async Task SendCode_RejectsEmailOutsidePilotList()
    {
        var (service, _) = await CreateServiceAsync(["pilot@example.com"]);

        var result = await service.SendCodeAsync("blocked@example.com", CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(SendCodeStatus.NotInvited, result.Status);
    }

    [Fact]
    public async Task SendCode_AllowsConfiguredAdminWithoutInvite()
    {
        var (service, _) = await CreateServiceAsync([], adminEmails: ["admin@example.com"]);

        var result = await service.SendCodeAsync("admin@example.com", CancellationToken.None);

        Assert.True(result.Accepted);
        Assert.Equal(SendCodeStatus.Sent, result.Status);
    }

    [Fact]
    public async Task SendCode_StoresHashedCodeInDatabase()
    {
        var (service, sender) = await CreateServiceAsync(["pilot@example.com"]);

        var result = await service.SendCodeAsync("pilot@example.com", CancellationToken.None);

        Assert.True(result.Accepted);
        Assert.NotNull(sender.LastCode);
        Assert.Equal(6, sender.LastCode!.Length);
        Assert.True(sender.LastCode.All(char.IsDigit));
    }

    [Fact]
    public async Task SendCode_RateLimitsAfterMaxCodes()
    {
        var (service, _) = await CreateServiceAsync(["pilot@example.com"]);

        for (var i = 0; i < 3; i++)
        {
            var sent = await service.SendCodeAsync("pilot@example.com", CancellationToken.None);
            Assert.True(sent.Accepted);
        }

        var result = await service.SendCodeAsync("pilot@example.com", CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(SendCodeStatus.TooManyRequests, result.Status);
    }

    [Fact]
    public async Task SendCode_RateLimitResetsAfterWindow()
    {
        var (service, _) = await CreateServiceAsync(["pilot@example.com"]);

        for (var i = 0; i < 3; i++)
        {
            await service.SendCodeAsync("pilot@example.com", CancellationToken.None);
        }

        _timeProvider.Advance(TimeSpan.FromMinutes(11));

        var result = await service.SendCodeAsync("pilot@example.com", CancellationToken.None);
        Assert.True(result.Accepted);
    }

    [Fact]
    public async Task SendCode_InvalidatesPreviousCodesForSameEmail()
    {
        var (service, sender) = await CreateServiceAsync(["pilot@example.com"]);

        await service.SendCodeAsync("pilot@example.com", CancellationToken.None);
        var firstCode = sender.LastCode;

        await service.SendCodeAsync("pilot@example.com", CancellationToken.None);
        var secondCode = sender.LastCode;

        // First code should be invalidated — only second should verify
        var resultFirst = await service.VerifyCodeAsync("pilot@example.com", firstCode!, CancellationToken.None);
        Assert.False(resultFirst.Accepted);

        var resultSecond = await service.VerifyCodeAsync("pilot@example.com", secondCode!, CancellationToken.None);
        Assert.True(resultSecond.Accepted);
    }

    [Fact]
    public async Task SendCode_NormalizesEmail()
    {
        var (service, sender) = await CreateServiceAsync(["pilot@example.com"]);

        var result = await service.SendCodeAsync("  PILOT@Example.COM  ", CancellationToken.None);

        Assert.True(result.Accepted);
        Assert.NotNull(sender.LastCode);
    }

    // --- VerifyCode tests ---

    [Fact]
    public async Task VerifyCode_CreatesUserAndMatrixIdentity()
    {
        var (service, sender) = await CreateServiceAsync(["pilot@example.com"], matrixPrefix: "superchat");

        await service.SendCodeAsync("pilot@example.com", CancellationToken.None);

        var result = await service.VerifyCodeAsync("pilot@example.com", sender.LastCode!, CancellationToken.None);

        Assert.True(result.Accepted);
        Assert.NotNull(result.User);
        Assert.Equal("pilot@example.com", result.User!.Email);
    }

    [Fact]
    public async Task VerifyCode_RejectsWrongCode()
    {
        var (service, sender) = await CreateServiceAsync(["pilot@example.com"]);

        await service.SendCodeAsync("pilot@example.com", CancellationToken.None);
        var realCode = sender.LastCode!;
        var wrongCode = realCode == "000000" ? "111111" : "000000";

        var result = await service.VerifyCodeAsync("pilot@example.com", wrongCode, CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(AuthVerificationStatus.InvalidOrExpired, result.Status);
    }

    [Fact]
    public async Task VerifyCode_RejectsAfterTooManyAttempts()
    {
        var (service, _) = await CreateServiceAsync(["pilot@example.com"], maxAttempts: 3);

        await service.SendCodeAsync("pilot@example.com", CancellationToken.None);

        for (var i = 0; i < 3; i++)
        {
            var r = await service.VerifyCodeAsync("pilot@example.com", "999999", CancellationToken.None);
            Assert.Equal(AuthVerificationStatus.InvalidOrExpired, r.Status);
        }

        var result = await service.VerifyCodeAsync("pilot@example.com", "999999", CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(AuthVerificationStatus.TooManyAttempts, result.Status);
    }

    [Fact]
    public async Task VerifyCode_RejectsExpiredCode()
    {
        var (service, sender) = await CreateServiceAsync(["pilot@example.com"], codeMinutes: 5);

        await service.SendCodeAsync("pilot@example.com", CancellationToken.None);
        var code = sender.LastCode!;

        _timeProvider.Advance(TimeSpan.FromMinutes(6));

        var result = await service.VerifyCodeAsync("pilot@example.com", code, CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(AuthVerificationStatus.InvalidOrExpired, result.Status);
    }

    [Fact]
    public async Task VerifyCode_RejectsCorrectCodeWithWrongEmail()
    {
        var (service, sender) = await CreateServiceAsync(["alice@example.com", "bob@example.com"]);

        await service.SendCodeAsync("alice@example.com", CancellationToken.None);
        var aliceCode = sender.LastCode!;

        var result = await service.VerifyCodeAsync("bob@example.com", aliceCode, CancellationToken.None);

        Assert.False(result.Accepted);
    }

    [Fact]
    public async Task VerifyCode_RejectsConsumedCode()
    {
        var (service, sender) = await CreateServiceAsync(["pilot@example.com"]);

        await service.SendCodeAsync("pilot@example.com", CancellationToken.None);
        var code = sender.LastCode!;

        var first = await service.VerifyCodeAsync("pilot@example.com", code, CancellationToken.None);
        Assert.True(first.Accepted);

        var second = await service.VerifyCodeAsync("pilot@example.com", code, CancellationToken.None);
        Assert.False(second.Accepted);
        Assert.Equal(AuthVerificationStatus.InvalidOrExpired, second.Status);
    }

    [Fact]
    public async Task VerifyCode_UpdatesLastSeenAtForExistingUser()
    {
        var (service, sender) = await CreateServiceAsync(["pilot@example.com"]);

        // First login
        await service.SendCodeAsync("pilot@example.com", CancellationToken.None);
        var first = await service.VerifyCodeAsync("pilot@example.com", sender.LastCode!, CancellationToken.None);
        var firstSeenAt = first.User!.LastSeenAt;

        _timeProvider.Advance(TimeSpan.FromMinutes(5));

        // Second login
        await service.SendCodeAsync("pilot@example.com", CancellationToken.None);
        var second = await service.VerifyCodeAsync("pilot@example.com", sender.LastCode!, CancellationToken.None);

        Assert.True(second.Accepted);
        Assert.Equal(first.User!.Id, second.User!.Id);
        Assert.True(second.User.LastSeenAt > firstSeenAt);
    }

    // --- helpers ---

    private async Task<(AuthFlowService Service, CapturingCodeSender Sender)> CreateServiceAsync(
        IReadOnlyList<string> invitedEmails,
        string[]? adminEmails = null,
        string? matrixPrefix = null,
        int codeMinutes = 10,
        int maxAttempts = 5)
    {
        var options = new PilotOptions
        {
            BaseUrl = "https://localhost:8080",
            VerificationCodeMinutes = codeMinutes,
            MaxVerificationAttempts = maxAttempts,
            AdminEmails = adminEmails ?? []
        };

        var factory = await CreateFactoryAsync(invitedEmails, CancellationToken.None);
        var matrix = new BootstrapMatrixProvisioningService(factory, new MatrixOptions { UserIdPrefix = matrixPrefix ?? "superchat" }, _timeProvider);
        var codeSender = new CapturingCodeSender();
        var logger = NullLogger<AuthFlowService>.Instance;
        var service = new AuthFlowService(factory, matrix, codeSender, options, _timeProvider, logger);
        return (service, codeSender);
    }

    private static async Task<IDbContextFactory<SuperChatDbContext>> CreateFactoryAsync(
        IReadOnlyList<string> invitedEmails,
        CancellationToken cancellationToken)
    {
        var dbContextOptions = new DbContextOptionsBuilder<SuperChatDbContext>()
            .UseInMemoryDatabase($"superchat-auth-{Guid.NewGuid():N}")
            .Options;

        var factory = new TestDbContextFactory(dbContextOptions);
        await using var dbContext = await factory.CreateDbContextAsync(cancellationToken);
        await dbContext.Database.EnsureCreatedAsync(cancellationToken);

        dbContext.PilotInvites.AddRange(invitedEmails.Select(email => new PilotInviteEntity
        {
            Email = email.Trim().ToLowerInvariant(),
            InvitedBy = "test",
            InvitedAt = DateTimeOffset.UtcNow,
            IsActive = true
        }));

        await dbContext.SaveChangesAsync(cancellationToken);
        return factory;
    }

    private sealed class TestDbContextFactory(DbContextOptions<SuperChatDbContext> options) : IDbContextFactory<SuperChatDbContext>
    {
        public SuperChatDbContext CreateDbContext() => new(options);
        public Task<SuperChatDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) => Task.FromResult(new SuperChatDbContext(options));
    }

    private sealed class CapturingCodeSender : IVerificationCodeSender
    {
        public string? LastCode { get; private set; }

        public Task SendAsync(string email, string code, CancellationToken cancellationToken)
        {
            LastCode = code;
            return Task.CompletedTask;
        }
    }
}
