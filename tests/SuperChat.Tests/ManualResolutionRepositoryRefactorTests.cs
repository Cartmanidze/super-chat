using Microsoft.EntityFrameworkCore;
using SuperChat.Domain.Features.Intelligence;
using SuperChat.Infrastructure.Features.Intelligence.Meetings;
using SuperChat.Infrastructure.Features.Intelligence.WorkItems;
using SuperChat.Infrastructure.Shared.Persistence;
using SuperChat.Infrastructure.Shared.Presentation;

namespace SuperChat.Tests;

public sealed class ManualResolutionRepositoryRefactorTests
{
    [Fact]
    public async Task MeetingRepository_ResolveRelatedAsync_ResolvesAllMeetingsWithSameSourceEventId()
    {
        var factory = await CreateFactoryAsync(CancellationToken.None);
        dynamic repository = new EfMeetingRepository(factory);
        var userId = Guid.NewGuid();
        const string sourceEventId = "$shared-meeting";
        var resolvedAt = new DateTimeOffset(2026, 03, 17, 10, 15, 00, TimeSpan.Zero);

        await using (var dbContext = await factory.CreateDbContextAsync(CancellationToken.None))
        {
            dbContext.Meetings.AddRange(
            [
                new MeetingEntity
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    Title = "Intro call",
                    Summary = "Call with product team",
                    ExternalChatId = "!team:matrix.localhost",
                    SourceEventId = sourceEventId,
                    ObservedAt = resolvedAt.AddHours(-2),
                    ScheduledFor = resolvedAt.AddHours(2),
                    Confidence = 0.77,
                    CreatedAt = resolvedAt.AddHours(-2),
                    UpdatedAt = resolvedAt.AddHours(-2)
                },
                new MeetingEntity
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    Title = "Intro call duplicate",
                    Summary = "Call with product team",
                    ExternalChatId = "!team:matrix.localhost",
                    SourceEventId = sourceEventId,
                    ObservedAt = resolvedAt.AddHours(-1),
                    ScheduledFor = resolvedAt.AddHours(2),
                    Confidence = 0.82,
                    CreatedAt = resolvedAt.AddHours(-1),
                    UpdatedAt = resolvedAt.AddHours(-1)
                },
                new MeetingEntity
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    Title = "Different meeting",
                    Summary = "Independent meeting",
                    ExternalChatId = "!team:matrix.localhost",
                    SourceEventId = "$other-meeting",
                    ObservedAt = resolvedAt.AddHours(-1),
                    ScheduledFor = resolvedAt.AddHours(3),
                    Confidence = 0.65,
                    CreatedAt = resolvedAt.AddHours(-1),
                    UpdatedAt = resolvedAt.AddHours(-1)
                }
            ]);

            await dbContext.SaveChangesAsync(CancellationToken.None);
        }

        await repository.ResolveRelatedAsync(
            userId,
            sourceEventId,
            WorkItemResolutionState.Dismissed,
            WorkItemResolutionState.Manual,
            resolvedAt,
            CancellationToken.None);

        await using var verificationContext = await factory.CreateDbContextAsync(CancellationToken.None);
        var relatedMeetings = await verificationContext.Meetings
            .Where(item => item.UserId == userId && item.SourceEventId == sourceEventId)
            .OrderBy(item => item.CreatedAt)
            .ToListAsync(CancellationToken.None);
        var unrelatedMeeting = await verificationContext.Meetings
            .SingleAsync(item => item.UserId == userId && item.SourceEventId == "$other-meeting", CancellationToken.None);

        Assert.All(relatedMeetings, meeting =>
        {
            Assert.Equal(resolvedAt, meeting.ResolvedAt);
            Assert.Equal(WorkItemResolutionState.Dismissed, meeting.ResolutionKind);
            Assert.Equal(WorkItemResolutionState.Manual, meeting.ResolutionSource);
            Assert.Equal(resolvedAt, meeting.UpdatedAt);
        });
        Assert.Null(unrelatedMeeting.ResolvedAt);
    }

    [Fact]
    public async Task WorkItemRepository_ResolveRelatedAsync_ResolvesAllItemsWithSameSourceEventId()
    {
        var factory = await CreateFactoryAsync(CancellationToken.None);
        dynamic repository = new EfWorkItemRepository(factory);
        var userId = Guid.NewGuid();
        const string sourceEventId = "$shared-item";
        var resolvedAt = new DateTimeOffset(2026, 03, 17, 10, 15, 00, TimeSpan.Zero);

        await using (var dbContext = await factory.CreateDbContextAsync(CancellationToken.None))
        {
            dbContext.WorkItems.AddRange(
            [
                new WorkItemEntity
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    Kind = ExtractedItemKind.Meeting,
                    Title = "Send contract",
                    Summary = "Please send the contract tomorrow.",
                    ExternalChatId = "!sales:matrix.localhost",
                    SourceEventId = sourceEventId,
                    ObservedAt = resolvedAt.AddHours(-2),
                    DueAt = resolvedAt.AddHours(5),
                    Confidence = 0.77,
                    CreatedAt = resolvedAt.AddHours(-2),
                    UpdatedAt = resolvedAt.AddHours(-2)
                },
                new WorkItemEntity
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    Kind = ExtractedItemKind.Meeting,
                    Title = "Send contract duplicate",
                    Summary = "Please send the contract tomorrow.",
                    ExternalChatId = "!sales:matrix.localhost",
                    SourceEventId = sourceEventId,
                    ObservedAt = resolvedAt.AddHours(-1),
                    DueAt = resolvedAt.AddHours(5),
                    Confidence = 0.82,
                    CreatedAt = resolvedAt.AddHours(-1),
                    UpdatedAt = resolvedAt.AddHours(-1)
                },
                new WorkItemEntity
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    Kind = ExtractedItemKind.Meeting,
                    Title = "Prepare deck",
                    Summary = "Prepare the new deck.",
                    ExternalChatId = "!sales:matrix.localhost",
                    SourceEventId = "$other-item",
                    ObservedAt = resolvedAt.AddHours(-1),
                    DueAt = resolvedAt.AddHours(6),
                    Confidence = 0.65,
                    CreatedAt = resolvedAt.AddHours(-1),
                    UpdatedAt = resolvedAt.AddHours(-1)
                }
            ]);

            await dbContext.SaveChangesAsync(CancellationToken.None);
        }

        await repository.ResolveRelatedAsync(
            userId,
            sourceEventId,
            WorkItemResolutionState.Completed,
            WorkItemResolutionState.Manual,
            resolvedAt,
            CancellationToken.None);

        await using var verificationContext = await factory.CreateDbContextAsync(CancellationToken.None);
        var relatedItems = await verificationContext.WorkItems
            .Where(item => item.UserId == userId && item.SourceEventId == sourceEventId)
            .OrderBy(item => item.CreatedAt)
            .ToListAsync(CancellationToken.None);
        var unrelatedItem = await verificationContext.WorkItems
            .SingleAsync(item => item.UserId == userId && item.SourceEventId == "$other-item", CancellationToken.None);

        Assert.All(relatedItems, item =>
        {
            Assert.Equal(resolvedAt, item.ResolvedAt);
            Assert.Equal(WorkItemResolutionState.Completed, item.ResolutionKind);
            Assert.Equal(WorkItemResolutionState.Manual, item.ResolutionSource);
            Assert.Equal(resolvedAt, item.UpdatedAt);
        });
        Assert.Null(unrelatedItem.ResolvedAt);
    }

    private static async Task<IDbContextFactory<SuperChatDbContext>> CreateFactoryAsync(CancellationToken cancellationToken)
    {
        var dbContextOptions = new DbContextOptionsBuilder<SuperChatDbContext>()
            .UseInMemoryDatabase($"superchat-manual-resolution-refactor-{Guid.NewGuid():N}")
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
