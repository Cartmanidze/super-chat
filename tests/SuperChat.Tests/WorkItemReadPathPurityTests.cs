using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SuperChat.Contracts.Features.Intelligence.Meetings;
using SuperChat.Domain.Features.Intelligence;
using SuperChat.Infrastructure.Features.Intelligence.Meetings;
using SuperChat.Infrastructure.Features.Intelligence.WorkItems;
using SuperChat.Infrastructure.Shared.Persistence;

namespace SuperChat.Tests;

public sealed class WorkItemReadPathPurityTests
{
    [Fact]
    public async Task WorkItemService_GetActiveForUserAsync_DoesNotMutateStateOnRead()
    {
        var factory = await CreateFactoryAsync(CancellationToken.None);
        var userId = Guid.NewGuid();
        var observedAt = new DateTimeOffset(2026, 03, 16, 08, 00, 00, TimeSpan.Zero);
        var itemId = Guid.NewGuid();

        await using (var dbContext = await factory.CreateDbContextAsync(CancellationToken.None))
        {
            dbContext.WorkItems.Add(new WorkItemEntity
            {
                Id = itemId,
                UserId = userId,
                Kind = ExtractedItemKind.WaitingOn,
                Title = "Need to reply",
                Summary = "Marina is waiting for the answer.",
                SourceRoom = "!sales:matrix.localhost",
                SourceEventId = "$evt-waiting",
                ObservedAt = observedAt,
                Confidence = 0.91
            });

            dbContext.NormalizedMessages.Add(new NormalizedMessageEntity
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Source = "telegram",
                ExternalChatId = "!sales:matrix.localhost",
                ExternalMessageId = "$evt-reply",
                SenderName = "You",
                Text = "I will send the answer in an hour.",
                SentAt = observedAt.AddMinutes(5),
                ReceivedAt = observedAt.AddMinutes(5),
                Processed = true
            });

            await dbContext.SaveChangesAsync(CancellationToken.None);
        }

        var service = CreateService(factory);
        var items = await service.GetActiveForUserAsync(userId, CancellationToken.None);

        var item = Assert.Single(items);
        Assert.Equal(itemId, item.Id);

        await using var verificationContext = await factory.CreateDbContextAsync(CancellationToken.None);
        var entity = await verificationContext.WorkItems.SingleAsync(workItem => workItem.Id == itemId, CancellationToken.None);
        Assert.Null(entity.ResolvedAt);
        Assert.Null(entity.ResolutionKind);
        Assert.Null(entity.ResolutionSource);
    }

    private static WorkItemService CreateService(IDbContextFactory<SuperChatDbContext> factory)
    {
        return new WorkItemService(
            new WorkItemWriter(factory, CreateMeetingService(factory), NullLogger<WorkItemWriter>.Instance),
            new EfWorkItemRepository(factory),
            TimeProvider.System);
    }

    private static MeetingService CreateMeetingService(IDbContextFactory<SuperChatDbContext> factory)
    {
        return new MeetingService(
            new MeetingUpsertService(factory, NullLogger<MeetingUpsertService>.Instance),
            new EfMeetingRepository(factory),
            TimeProvider.System);
    }

    private static async Task<IDbContextFactory<SuperChatDbContext>> CreateFactoryAsync(CancellationToken cancellationToken)
    {
        var dbContextOptions = new DbContextOptionsBuilder<SuperChatDbContext>()
            .UseInMemoryDatabase($"superchat-work-item-read-purity-{Guid.NewGuid():N}")
            .Options;

        var factory = new TestDbContextFactory(dbContextOptions);
        await using var dbContext = await factory.CreateDbContextAsync(cancellationToken);
        await dbContext.Database.EnsureCreatedAsync(cancellationToken);
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
