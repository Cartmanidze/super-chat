using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SuperChat.Domain.Features.Intelligence;
using SuperChat.Infrastructure.Features.Intelligence.Meetings;
using SuperChat.Infrastructure.Features.Intelligence.WorkItems;
using SuperChat.Infrastructure.Shared.Persistence;

namespace SuperChat.Tests;

public sealed class WorkItemIngestionServiceTests
{
    [Fact]
    public async Task IngestRangeAsync_DeduplicatesBatchByUserSourceEventIdAndKind_KeepingHighestConfidence()
    {
        var factory = await CreateFactoryAsync(CancellationToken.None);
        var userId = Guid.NewGuid();
        var observedAt = new DateTimeOffset(2026, 04, 07, 09, 00, 00, TimeSpan.Zero);
        var roomId = "!sales:matrix.localhost";
        var sourceEventId = "$evt-task";

        var service = new WorkItemIngestionService(
            factory,
            CreateMeetingService(factory),
            NullLogger<WorkItemIngestionService>.Instance);

        await service.IngestRangeAsync(
        [
            new ExtractedItem(
                Guid.NewGuid(),
                userId,
                ExtractedItemKind.Task,
                "Отправить смету",
                "Отправить смету клиенту",
                roomId,
                sourceEventId,
                null,
                observedAt,
                null,
                new Confidence(0.61)),
            new ExtractedItem(
                Guid.NewGuid(),
                userId,
                ExtractedItemKind.Task,
                "Отправить смету",
                "Отправить смету клиенту",
                roomId,
                sourceEventId,
                null,
                observedAt,
                null,
                new Confidence(0.92))
        ], CancellationToken.None);

        await using var verificationContext = await factory.CreateDbContextAsync(CancellationToken.None);
        var items = await verificationContext.WorkItems
            .Where(item => item.UserId == userId)
            .ToListAsync(CancellationToken.None);

        var item = Assert.Single(items);
        Assert.Equal(0.92, item.Confidence);
    }

    [Fact]
    public async Task IngestRangeAsync_SkipsExistingWorkItemWithSameUserSourceEventIdAndKind()
    {
        var factory = await CreateFactoryAsync(CancellationToken.None);
        var userId = Guid.NewGuid();
        var observedAt = new DateTimeOffset(2026, 04, 07, 09, 00, 00, TimeSpan.Zero);
        var roomId = "!sales:matrix.localhost";
        var sourceEventId = "$evt-task";

        await using (var dbContext = await factory.CreateDbContextAsync(CancellationToken.None))
        {
            dbContext.WorkItems.Add(new WorkItemEntity
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Kind = ExtractedItemKind.Task,
                Title = "Отправить смету",
                Summary = "Отправить смету клиенту",
                SourceRoom = roomId,
                SourceEventId = sourceEventId,
                ObservedAt = observedAt,
                Confidence = 0.70,
                CreatedAt = observedAt,
                UpdatedAt = observedAt
            });

            await dbContext.SaveChangesAsync(CancellationToken.None);
        }

        var service = new WorkItemIngestionService(
            factory,
            CreateMeetingService(factory),
            NullLogger<WorkItemIngestionService>.Instance);

        await service.IngestRangeAsync(
        [
            new ExtractedItem(
                Guid.NewGuid(),
                userId,
                ExtractedItemKind.Task,
                "Отправить смету",
                "Отправить смету клиенту",
                roomId,
                sourceEventId,
                null,
                observedAt.AddMinutes(1),
                null,
                new Confidence(0.95))
        ], CancellationToken.None);

        await using var verificationContext = await factory.CreateDbContextAsync(CancellationToken.None);
        var items = await verificationContext.WorkItems
            .Where(item => item.UserId == userId)
            .OrderBy(item => item.CreatedAt)
            .ToListAsync(CancellationToken.None);

        Assert.Single(items);
    }

    private static async Task<IDbContextFactory<SuperChatDbContext>> CreateFactoryAsync(CancellationToken cancellationToken)
    {
        var dbContextOptions = new DbContextOptionsBuilder<SuperChatDbContext>()
            .UseInMemoryDatabase($"superchat-work-item-ingestion-{Guid.NewGuid():N}")
            .Options;

        var factory = new TestDbContextFactory(dbContextOptions);
        await using var dbContext = await factory.CreateDbContextAsync(cancellationToken);
        await dbContext.Database.EnsureCreatedAsync(cancellationToken);
        return factory;
    }

    private static MeetingService CreateMeetingService(IDbContextFactory<SuperChatDbContext> factory)
    {
        return new MeetingService(
            new MeetingUpsertService(factory, NullLogger<MeetingUpsertService>.Instance),
            new EfMeetingRepository(factory),
            TimeProvider.System);
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
