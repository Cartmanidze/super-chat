using Microsoft.EntityFrameworkCore;
using SuperChat.Contracts.Features.Auth;
using SuperChat.Contracts.Features.Integrations.Matrix;
using SuperChat.Infrastructure.Features.Auth;
using SuperChat.Infrastructure.Features.Integrations.Matrix;
using SuperChat.Infrastructure.Shared.Persistence;

namespace SuperChat.Tests;

public sealed class AuthFlowServiceTests
{
    [Fact]
    public async Task RequestMagicLink_RejectsEmailOutsidePilotList()
    {
        var options = new PilotOptions
        {
            BaseUrl = "https://localhost:8080",
            MagicLinkMinutes = 15
        };

        var factory = await CreateFactoryAsync(["pilot@example.com"], CancellationToken.None);
        var matrix = new BootstrapMatrixProvisioningService(factory, new MatrixOptions(), TimeProvider.System);
        var service = new AuthFlowService(factory, matrix, options, TimeProvider.System);

        var result = await service.RequestMagicLinkAsync("blocked@example.com", CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Null(result.DevelopmentLink);
    }

    [Fact]
    public async Task Verify_CreatesUserAndMatrixIdentity()
    {
        var options = new PilotOptions
        {
            BaseUrl = "https://localhost:8080",
            MagicLinkMinutes = 15
        };

        var factory = await CreateFactoryAsync(["pilot@example.com"], CancellationToken.None);
        var matrix = new BootstrapMatrixProvisioningService(factory, new MatrixOptions { UserIdPrefix = "superchat" }, TimeProvider.System);
        var service = new AuthFlowService(factory, matrix, options, TimeProvider.System);

        var linkResult = await service.RequestMagicLinkAsync("pilot@example.com", CancellationToken.None);
        var token = linkResult.DevelopmentLink!.Query.Split("token=", StringSplitOptions.TrimEntries)[1];

        var result = await service.VerifyAsync(token, CancellationToken.None);
        var identity = await matrix.GetIdentityAsync(result.User!.Id, CancellationToken.None);

        Assert.True(result.Accepted);
        Assert.NotNull(result.User);
        Assert.NotNull(identity);
        Assert.StartsWith("@superchat-pilot", identity!.MatrixUserId, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RequestMagicLink_AllowsConfiguredAdminWithoutInvite()
    {
        var options = new PilotOptions
        {
            BaseUrl = "https://localhost:8080",
            MagicLinkMinutes = 15,
            AdminEmails = ["admin@example.com"]
        };

        var factory = await CreateFactoryAsync([], CancellationToken.None);
        var matrix = new BootstrapMatrixProvisioningService(factory, new MatrixOptions(), TimeProvider.System);
        var service = new AuthFlowService(factory, matrix, options, TimeProvider.System);

        var result = await service.RequestMagicLinkAsync("admin@example.com", CancellationToken.None);

        Assert.True(result.Accepted);
        Assert.NotNull(result.DevelopmentLink);
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
