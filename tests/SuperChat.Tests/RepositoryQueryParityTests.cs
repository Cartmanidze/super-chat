using Microsoft.EntityFrameworkCore;
using SuperChat.Domain.Features.Intelligence;
using SuperChat.Infrastructure.Features.Intelligence.Meetings;
using SuperChat.Infrastructure.Features.Intelligence.WorkItems;
using SuperChat.Infrastructure.Shared.Persistence;

namespace SuperChat.Tests;

public sealed class RepositoryQueryParityTests
{
    [Fact]
    public async Task MeetingRepository_GetUpcomingAsync_DeduplicatesBeforeApplyingTakeAndKeepsBestCandidate()
    {
        var factory = await CreateFactoryAsync(CancellationToken.None);
        var repository = new EfMeetingRepository(factory);
        var userId = Guid.NewGuid();
        var scheduledFor = new DateTimeOffset(2026, 03, 13, 17, 00, 00, TimeSpan.Zero);

        await using (var dbContext = await factory.CreateDbContextAsync(CancellationToken.None))
        {
            dbContext.Meetings.AddRange(
            [
                new MeetingEntity
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    Title = "Lower confidence duplicate",
                    Summary = "Sync with product",
                    SourceRoom = "!team:matrix.localhost",
                    SourceEventId = "$meeting-low",
                    ObservedAt = scheduledFor.AddHours(-3),
                    ScheduledFor = scheduledFor,
                    Confidence = 0.61,
                    CreatedAt = scheduledFor.AddHours(-3),
                    UpdatedAt = scheduledFor.AddHours(-3)
                },
                new MeetingEntity
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    Title = "Higher confidence duplicate",
                    Summary = "Sync with product",
                    SourceRoom = "!team:matrix.localhost",
                    SourceEventId = "$meeting-high",
                    ObservedAt = scheduledFor.AddHours(-2),
                    ScheduledFor = scheduledFor,
                    Confidence = 0.93,
                    CreatedAt = scheduledFor.AddHours(-2),
                    UpdatedAt = scheduledFor.AddHours(-2)
                },
                new MeetingEntity
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    Title = "Customer review",
                    Summary = "Walk through status update",
                    SourceRoom = "!team:matrix.localhost",
                    SourceEventId = "$meeting-unique",
                    ObservedAt = scheduledFor.AddHours(-1),
                    ScheduledFor = scheduledFor.AddMinutes(30),
                    Confidence = 0.72,
                    CreatedAt = scheduledFor.AddHours(-1),
                    UpdatedAt = scheduledFor.AddHours(-1)
                }
            ]);

            await dbContext.SaveChangesAsync(CancellationToken.None);
        }

        var meetings = await repository.GetUpcomingAsync(
            userId,
            scheduledFor.AddHours(-1),
            2,
            CancellationToken.None);

        Assert.Collection(
            meetings,
            item => Assert.Equal("$meeting-high", item.SourceEventId),
            item => Assert.Equal("$meeting-unique", item.SourceEventId));
    }

    [Fact]
    public async Task WorkItemRepository_GetByUserAsync_FiltersLegacyFollowUpCandidates()
    {
        var factory = await CreateFactoryAsync(CancellationToken.None);
        var repository = new EfWorkItemRepository(factory);
        var userId = Guid.NewGuid();

        await using (var dbContext = await factory.CreateDbContextAsync(CancellationToken.None))
        {
            dbContext.WorkItems.AddRange(
            [
                new WorkItemEntity
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    Kind = ExtractedItemKind.Task,
                    Title = "Follow-up candidate",
                    Summary = "video.mp4",
                    SourceRoom = "!room:matrix.localhost",
                    SourceEventId = "$evt-legacy",
                    ObservedAt = DateTimeOffset.UtcNow,
                    Confidence = 0.51,
                    CreatedAt = DateTimeOffset.UtcNow,
                    UpdatedAt = DateTimeOffset.UtcNow
                },
                new WorkItemEntity
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    Kind = ExtractedItemKind.Task,
                    Title = "Send contract",
                    Summary = "Please send the contract tomorrow.",
                    SourceRoom = "!sales:matrix.localhost",
                    SourceEventId = "$evt-real",
                    ObservedAt = DateTimeOffset.UtcNow.AddMinutes(1),
                    Confidence = 0.91,
                    CreatedAt = DateTimeOffset.UtcNow.AddMinutes(1),
                    UpdatedAt = DateTimeOffset.UtcNow.AddMinutes(1)
                }
            ]);

            await dbContext.SaveChangesAsync(CancellationToken.None);
        }

        var items = await repository.GetByUserAsync(userId, unresolvedOnly: false, CancellationToken.None);

        var item = Assert.Single(items);
        Assert.Equal("$evt-real", item.SourceEventId);
    }

    [Fact]
    public async Task WorkItemRepository_GetByUserAsync_FiltersStructuredArtifacts()
    {
        var factory = await CreateFactoryAsync(CancellationToken.None);
        var repository = new EfWorkItemRepository(factory);
        var userId = Guid.NewGuid();
        var artifactText =
            """
            Design a high-fidelity desktop web app for invite-only onboarding and daily review.
            1. Product Goal:
            - connect telegram
            - main today view
            - search / ask
            2. Target Users:
            - founders
            - operators
            - analysts
            3. Main Information Architecture:
            - sidebar
            - top navigation
            - evidence snippet
            4. Visual Style:
            - wireframe
            - responsive mobile
            - screen 1
            """;

        await using (var dbContext = await factory.CreateDbContextAsync(CancellationToken.None))
        {
            dbContext.WorkItems.AddRange(
            [
                new WorkItemEntity
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    Kind = ExtractedItemKind.Task,
                    Title = "Generated design brief",
                    Summary = artifactText,
                    SourceRoom = "!design:matrix.localhost",
                    SourceEventId = "$evt-artifact",
                    ObservedAt = DateTimeOffset.UtcNow,
                    Confidence = 0.63,
                    CreatedAt = DateTimeOffset.UtcNow,
                    UpdatedAt = DateTimeOffset.UtcNow
                },
                new WorkItemEntity
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    Kind = ExtractedItemKind.Task,
                    Title = "Book customer interview",
                    Summary = "Coordinate the next interview slot with the customer.",
                    SourceRoom = "!research:matrix.localhost",
                    SourceEventId = "$evt-keep",
                    ObservedAt = DateTimeOffset.UtcNow.AddMinutes(1),
                    Confidence = 0.87,
                    CreatedAt = DateTimeOffset.UtcNow.AddMinutes(1),
                    UpdatedAt = DateTimeOffset.UtcNow.AddMinutes(1)
                }
            ]);

            await dbContext.SaveChangesAsync(CancellationToken.None);
        }

        var items = await repository.GetByUserAsync(userId, unresolvedOnly: false, CancellationToken.None);

        var item = Assert.Single(items);
        Assert.Equal("$evt-keep", item.SourceEventId);
    }

    private static async Task<IDbContextFactory<SuperChatDbContext>> CreateFactoryAsync(CancellationToken cancellationToken)
    {
        var dbContextOptions = new DbContextOptionsBuilder<SuperChatDbContext>()
            .UseInMemoryDatabase($"superchat-repository-query-parity-{Guid.NewGuid():N}")
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
