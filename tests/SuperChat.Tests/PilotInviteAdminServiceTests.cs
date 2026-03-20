using Microsoft.EntityFrameworkCore;
using SuperChat.Infrastructure.Features.Auth;
using SuperChat.Infrastructure.Shared.Persistence;

namespace SuperChat.Tests;

public sealed class PilotInviteAdminServiceTests
{
    [Fact]
    public async Task AddInviteAsync_AddsNewActiveInvite()
    {
        var factory = CreateFactory();
        var service = new PilotInviteAdminService(factory);

        var result = await service.AddInviteAsync("new@example.com", "admin@example.com", CancellationToken.None);
        var invites = await service.GetInvitesAsync(CancellationToken.None);

        Assert.True(result.Succeeded);
        var invite = Assert.Single(invites);
        Assert.Equal("new@example.com", invite.Email);
        Assert.Equal("admin@example.com", invite.InvitedBy);
        Assert.True(invite.IsActive);
    }

    [Fact]
    public async Task AddInviteAsync_ReactivatesInactiveInvite()
    {
        var factory = CreateFactory();
        await using (var dbContext = await factory.CreateDbContextAsync(CancellationToken.None))
        {
            dbContext.PilotInvites.Add(new PilotInviteEntity
            {
                Email = "returning@example.com",
                InvitedBy = "bootstrap",
                InvitedAt = DateTimeOffset.UtcNow.AddDays(-1),
                IsActive = false
            });

            await dbContext.SaveChangesAsync(CancellationToken.None);
        }

        var service = new PilotInviteAdminService(factory);
        var result = await service.AddInviteAsync("returning@example.com", "admin@example.com", CancellationToken.None);
        var invites = await service.GetInvitesAsync(CancellationToken.None);

        Assert.True(result.Succeeded);
        var invite = Assert.Single(invites);
        Assert.Equal("returning@example.com", invite.Email);
        Assert.Equal("admin@example.com", invite.InvitedBy);
        Assert.True(invite.IsActive);
    }

    [Fact]
    public async Task AddInviteAsync_RejectsInvalidEmail()
    {
        var factory = CreateFactory();
        var service = new PilotInviteAdminService(factory);

        var result = await service.AddInviteAsync("not-an-email", "admin@example.com", CancellationToken.None);
        var invites = await service.GetInvitesAsync(CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Empty(invites);
    }

    private static TestDbContextFactory CreateFactory()
    {
        var options = new DbContextOptionsBuilder<SuperChatDbContext>()
            .UseInMemoryDatabase($"superchat-invites-{Guid.NewGuid():N}")
            .Options;

        return new TestDbContextFactory(options);
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
