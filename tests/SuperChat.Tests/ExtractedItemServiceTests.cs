using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SuperChat.Contracts.Configuration;
using SuperChat.Contracts.ViewModels;
using SuperChat.Domain.Model;
using SuperChat.Infrastructure.Persistence;
using SuperChat.Infrastructure.Services;

namespace SuperChat.Tests;

public sealed class ExtractedItemServiceTests
{
    [Fact]
    public async Task AddRangeAsync_DoesNotPersistGenericFollowUpCandidate()
    {
        var factory = await CreateFactoryAsync(CancellationToken.None);
        var userId = Guid.NewGuid();
        var service = CreateService(factory);

        await service.AddRangeAsync(
        [
            new ExtractedItem(
                Guid.NewGuid(),
                userId,
                ExtractedItemKind.Task,
                "Follow-up candidate",
                "video.mp4",
                "!room:matrix.localhost",
                "$evt-1",
                null,
                DateTimeOffset.UtcNow,
                null,
                0.51)
        ],
        CancellationToken.None);

        await using var dbContext = await factory.CreateDbContextAsync(CancellationToken.None);
        var count = await dbContext.ExtractedItems.CountAsync(CancellationToken.None);

        Assert.Equal(0, count);
    }

    [Fact]
    public async Task GetForUserAsync_FiltersLegacyFollowUpCandidateItems()
    {
        var factory = await CreateFactoryAsync(CancellationToken.None);
        var userId = Guid.NewGuid();

        await using (var dbContext = await factory.CreateDbContextAsync(CancellationToken.None))
        {
            dbContext.ExtractedItems.AddRange(
            [
                new ExtractedItemEntity
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    Kind = ExtractedItemKind.Task,
                    Title = "Follow-up candidate",
                    Summary = "Обманул",
                    SourceRoom = "!room:matrix.localhost",
                    SourceEventId = "$evt-legacy",
                    ObservedAt = DateTimeOffset.UtcNow,
                    Confidence = 0.51
                },
                new ExtractedItemEntity
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    Kind = ExtractedItemKind.Task,
                    Title = "Send contract",
                    Summary = "Please send the contract tomorrow.",
                    SourceRoom = "!sales:matrix.localhost",
                    SourceEventId = "$evt-real",
                    ObservedAt = DateTimeOffset.UtcNow.AddMinutes(1),
                    Confidence = 0.91
                }
            ]);

            await dbContext.SaveChangesAsync(CancellationToken.None);
        }

        var service = CreateService(factory);
        var items = await service.GetForUserAsync(userId, CancellationToken.None);

        Assert.Single(items);
        Assert.Equal("Send contract", items[0].Title);
    }

    [Fact]
    public async Task AddRangeAsync_ProjectsMeetingsIntoSeparateTable()
    {
        var factory = await CreateFactoryAsync(CancellationToken.None);
        var userId = Guid.NewGuid();
        var service = CreateService(factory);
        var dueAt = new DateTimeOffset(2026, 03, 13, 11, 00, 00, TimeSpan.FromHours(6));

        await service.AddRangeAsync(
        [
            new ExtractedItem(
                Guid.NewGuid(),
                userId,
                ExtractedItemKind.Meeting,
                "Upcoming meeting",
                "Мб заехать за тобой в 11?",
                "!friends:matrix.localhost",
                "$evt-meeting",
                null,
                dueAt.AddHours(-1),
                dueAt,
                0.86)
        ],
        CancellationToken.None);

        await using var dbContext = await factory.CreateDbContextAsync(CancellationToken.None);
        var projectedMeeting = await dbContext.Meetings.SingleAsync(CancellationToken.None);

        Assert.Equal(userId, projectedMeeting.UserId);
        Assert.Equal("$evt-meeting", projectedMeeting.SourceEventId);
        Assert.Equal(dueAt.ToUniversalTime(), projectedMeeting.ScheduledFor);
        Assert.Equal(TimeSpan.Zero, projectedMeeting.ScheduledFor.Offset);
        Assert.Equal("Мб заехать за тобой в 11?", projectedMeeting.Summary);

        var meetings = await CreateMeetingService(factory).GetUpcomingAsync(userId, dueAt.AddHours(-2), 10, CancellationToken.None);
        Assert.Single(meetings);
        Assert.Equal(dueAt.ToUniversalTime(), meetings[0].ScheduledFor);
    }

    [Fact]
    public async Task AddRangeAsync_ProjectsMeetingJoinLinkIntoSeparateTable()
    {
        var factory = await CreateFactoryAsync(CancellationToken.None);
        var userId = Guid.NewGuid();
        var service = CreateService(factory);
        var dueAt = new DateTimeOffset(2026, 03, 13, 11, 00, 00, TimeSpan.FromHours(6));
        var joinUrl = new Uri("https://meet.google.com/abc-defg-hij");

        await service.AddRangeAsync(
        [
            new ExtractedItem(
                Guid.NewGuid(),
                userId,
                ExtractedItemKind.Meeting,
                "Upcoming meeting",
                $"Созвон в 11, подключение тут {joinUrl}",
                "!friends:matrix.localhost",
                "$evt-meeting-link",
                null,
                dueAt.AddHours(-1),
                dueAt,
                0.93)
        ],
        CancellationToken.None);

        await using var dbContext = await factory.CreateDbContextAsync(CancellationToken.None);
        var projectedMeeting = await dbContext.Meetings.SingleAsync(CancellationToken.None);

        Assert.Equal("GoogleMeet", projectedMeeting.MeetingProvider);
        Assert.Equal(joinUrl.ToString(), projectedMeeting.MeetingJoinUrl);

        var meetings = await CreateMeetingService(factory).GetUpcomingAsync(userId, dueAt.AddHours(-2), 10, CancellationToken.None);
        var meeting = Assert.Single(meetings);
        Assert.Equal("GoogleMeet", meeting.MeetingProvider);
        Assert.Equal(joinUrl, meeting.MeetingJoinUrl);
    }

    [Fact]
    public async Task GetActiveForUserAsync_AutoResolvesWaitingOnWhenUserReplies()
    {
        var factory = await CreateFactoryAsync(CancellationToken.None);
        var userId = Guid.NewGuid();
        var observedAt = new DateTimeOffset(2026, 03, 16, 08, 00, 00, TimeSpan.Zero);
        var itemId = Guid.NewGuid();

        await using (var dbContext = await factory.CreateDbContextAsync(CancellationToken.None))
        {
            dbContext.ExtractedItems.Add(new ExtractedItemEntity
            {
                Id = itemId,
                UserId = userId,
                Kind = ExtractedItemKind.WaitingOn,
                Title = "Need to reply",
                Summary = "Marina is waiting for the answer.",
                SourceRoom = "!sales:matrix.localhost",
                SourceEventId = "$evt-waiting",
                Person = "Marina",
                ObservedAt = observedAt,
                Confidence = 0.91
            });

            dbContext.NormalizedMessages.Add(new NormalizedMessageEntity
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Source = "telegram",
                MatrixRoomId = "!sales:matrix.localhost",
                MatrixEventId = "$evt-reply",
                SenderName = "You",
                Text = "I will send the answer in an hour.",
                SentAt = observedAt.AddMinutes(5),
                IngestedAt = observedAt.AddMinutes(5),
                Processed = true
            });

            await dbContext.SaveChangesAsync(CancellationToken.None);
        }

        var service = CreateService(factory);
        var items = await service.GetActiveForUserAsync(userId, CancellationToken.None);

        Assert.Empty(items);

        await using var verificationContext = await factory.CreateDbContextAsync(CancellationToken.None);
        var entity = await verificationContext.ExtractedItems.SingleAsync(item => item.Id == itemId, CancellationToken.None);
        Assert.NotNull(entity.ResolvedAt);
        Assert.Equal(WorkItemResolutionState.Completed, entity.ResolutionKind);
        Assert.Equal(WorkItemResolutionState.AutoReply, entity.ResolutionSource);
    }

    [Fact]
    public async Task GetActiveForUserAsync_AutoResolvesCommitmentWhenCompletionMessageAppears()
    {
        var factory = await CreateFactoryAsync(CancellationToken.None);
        var userId = Guid.NewGuid();
        var observedAt = new DateTimeOffset(2026, 03, 16, 09, 00, 00, TimeSpan.Zero);
        var itemId = Guid.NewGuid();

        await using (var dbContext = await factory.CreateDbContextAsync(CancellationToken.None))
        {
            dbContext.ExtractedItems.Add(new ExtractedItemEntity
            {
                Id = itemId,
                UserId = userId,
                Kind = ExtractedItemKind.Commitment,
                Title = "You promised to send the deck",
                Summary = "Need to send the final deck.",
                SourceRoom = "!sales:matrix.localhost",
                SourceEventId = "$evt-commitment",
                ObservedAt = observedAt,
                Confidence = 0.88
            });

            dbContext.NormalizedMessages.Add(new NormalizedMessageEntity
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Source = "telegram",
                MatrixRoomId = "!sales:matrix.localhost",
                MatrixEventId = "$evt-done",
                SenderName = "You",
                Text = "готово, отправил финальный дек",
                SentAt = observedAt.AddMinutes(7),
                IngestedAt = observedAt.AddMinutes(7),
                Processed = true
            });

            await dbContext.SaveChangesAsync(CancellationToken.None);
        }

        var service = CreateService(factory);
        var items = await service.GetActiveForUserAsync(userId, CancellationToken.None);

        Assert.Empty(items);

        await using var verificationContext = await factory.CreateDbContextAsync(CancellationToken.None);
        var entity = await verificationContext.ExtractedItems.SingleAsync(item => item.Id == itemId, CancellationToken.None);
        Assert.NotNull(entity.ResolvedAt);
        Assert.Equal(WorkItemResolutionState.Completed, entity.ResolutionKind);
        Assert.Equal(WorkItemResolutionState.AutoCompletion, entity.ResolutionSource);
    }

    [Fact]
    public async Task GetUpcomingAsync_AutoResolvesMeetingWhenLaterMessageConfirmsItFinished()
    {
        var factory = await CreateFactoryAsync(CancellationToken.None);
        var userId = Guid.NewGuid();
        var scheduledFor = new DateTimeOffset(2026, 03, 16, 12, 00, 00, TimeSpan.Zero);
        var meetingId = Guid.NewGuid();

        await using (var dbContext = await factory.CreateDbContextAsync(CancellationToken.None))
        {
            dbContext.Meetings.Add(new MeetingEntity
            {
                Id = meetingId,
                UserId = userId,
                Title = "Sync",
                Summary = "Call at noon",
                SourceRoom = "!team:matrix.localhost",
                SourceEventId = "$evt-meeting-finished",
                ObservedAt = scheduledFor.AddHours(-1),
                ScheduledFor = scheduledFor,
                Confidence = 0.93,
                CreatedAt = scheduledFor.AddHours(-1),
                UpdatedAt = scheduledFor.AddHours(-1)
            });

            dbContext.NormalizedMessages.Add(new NormalizedMessageEntity
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Source = "telegram",
                MatrixRoomId = "!team:matrix.localhost",
                MatrixEventId = "$evt-after-call",
                SenderName = "Alex",
                Text = "Thanks for the call, I will follow up with notes.",
                SentAt = scheduledFor.AddMinutes(15),
                IngestedAt = scheduledFor.AddMinutes(15),
                Processed = true
            });

            await dbContext.SaveChangesAsync(CancellationToken.None);
        }

        var meetings = await CreateMeetingService(factory).GetUpcomingAsync(
            userId,
            scheduledFor.AddHours(-2),
            10,
            CancellationToken.None);

        Assert.Empty(meetings);

        await using var verificationContext = await factory.CreateDbContextAsync(CancellationToken.None);
        var entity = await verificationContext.Meetings.SingleAsync(item => item.Id == meetingId, CancellationToken.None);
        Assert.NotNull(entity.ResolvedAt);
        Assert.Equal(WorkItemResolutionState.Completed, entity.ResolutionKind);
        Assert.Equal(WorkItemResolutionState.AutoMeetingCompletion, entity.ResolutionSource);
    }

    [Fact]
    public async Task WorkItemActionService_DismissAsync_ResolvesMeetingAndRelatedExtractedItemsBySourceEventId()
    {
        var factory = await CreateFactoryAsync(CancellationToken.None);
        var userId = Guid.NewGuid();
        var sourceEventId = "$evt-shared-meeting";
        var extractedItemId = Guid.NewGuid();
        var meetingId = Guid.NewGuid();
        var scheduledFor = new DateTimeOffset(2026, 03, 17, 10, 00, 00, TimeSpan.Zero);

        await using (var dbContext = await factory.CreateDbContextAsync(CancellationToken.None))
        {
            dbContext.ExtractedItems.Add(new ExtractedItemEntity
            {
                Id = extractedItemId,
                UserId = userId,
                Kind = ExtractedItemKind.Meeting,
                Title = "Intro call",
                Summary = "Call with product team",
                SourceRoom = "!team:matrix.localhost",
                SourceEventId = sourceEventId,
                ObservedAt = scheduledFor.AddHours(-2),
                DueAt = scheduledFor,
                Confidence = 0.77
            });

            dbContext.Meetings.Add(new MeetingEntity
            {
                Id = meetingId,
                UserId = userId,
                Title = "Intro call",
                Summary = "Call with product team",
                SourceRoom = "!team:matrix.localhost",
                SourceEventId = sourceEventId,
                ObservedAt = scheduledFor.AddHours(-2),
                ScheduledFor = scheduledFor,
                Confidence = 0.77,
                CreatedAt = scheduledFor.AddHours(-2),
                UpdatedAt = scheduledFor.AddHours(-2)
            });

            await dbContext.SaveChangesAsync(CancellationToken.None);
        }

        var actionService = CreateActionService(factory);
        var result = await actionService.DismissAsync(
            userId,
            WorkItemType.Event,
            WorkItemActionKey.ForMeeting(meetingId),
            CancellationToken.None);

        Assert.True(result);

        await using var verificationContext = await factory.CreateDbContextAsync(CancellationToken.None);
        var extracted = await verificationContext.ExtractedItems.SingleAsync(item => item.Id == extractedItemId, CancellationToken.None);
        var meeting = await verificationContext.Meetings.SingleAsync(item => item.Id == meetingId, CancellationToken.None);

        Assert.NotNull(extracted.ResolvedAt);
        Assert.Equal(WorkItemResolutionState.Dismissed, extracted.ResolutionKind);
        Assert.Equal(WorkItemResolutionState.Manual, extracted.ResolutionSource);
        Assert.NotNull(meeting.ResolvedAt);
        Assert.Equal(WorkItemResolutionState.Dismissed, meeting.ResolutionKind);
        Assert.Equal(WorkItemResolutionState.Manual, meeting.ResolutionSource);
    }

    [Fact]
    public async Task GetUpcomingAsync_ReturnsProjectedChunkMeetings()
    {
        var factory = await CreateFactoryAsync(CancellationToken.None);
        var userId = Guid.NewGuid();

        await using (var dbContext = await factory.CreateDbContextAsync(CancellationToken.None))
        {
            dbContext.MessageChunks.Add(new MessageChunkEntity
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Source = "telegram",
                Provider = "telegram",
                Transport = "matrix_bridge",
                ChatId = "!friends:matrix.localhost",
                Kind = "dialog_chunk",
                Text = """
                    Alex: давай зафиксируем
                    You: итого, у нас будет встреча в 20:00 по мск времени сегодня, подтверждаю это
                    Alex: ок
                    """,
                MessageCount = 3,
                TsFrom = new DateTimeOffset(2026, 03, 13, 09, 00, 00, TimeSpan.Zero),
                TsTo = new DateTimeOffset(2026, 03, 13, 09, 15, 00, TimeSpan.Zero),
                ContentHash = "chunk-hash-meeting",
                ChunkVersion = 1,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            });

            await dbContext.SaveChangesAsync(CancellationToken.None);
        }

        var projectionService = new MeetingProjectionService(
            factory,
            Options.Create(new MeetingProjectionOptions
            {
                Enabled = true
            }),
            CreatePilotOptions(),
            TimeProvider.System);

        var projectionResult = await projectionService.ProjectPendingChunkMeetingsAsync(CancellationToken.None);
        Assert.Equal(1, projectionResult.MeetingsProjected);

        var meetings = await CreateMeetingService(factory).GetUpcomingAsync(
            userId,
            new DateTimeOffset(2026, 03, 13, 08, 00, 00, TimeSpan.Zero),
            10,
            CancellationToken.None);

        var meeting = Assert.Single(meetings);
        Assert.Equal("итого, у нас будет встреча в 20:00 по мск времени сегодня, подтверждаю это", meeting.Summary);
        Assert.Equal(new DateTimeOffset(2026, 03, 13, 17, 00, 00, TimeSpan.Zero), meeting.ScheduledFor);
        Assert.StartsWith("chunk:", meeting.SourceEventId, StringComparison.Ordinal);
    }

    private static async Task<IDbContextFactory<SuperChatDbContext>> CreateFactoryAsync(CancellationToken cancellationToken)
    {
        var dbContextOptions = new DbContextOptionsBuilder<SuperChatDbContext>()
            .UseInMemoryDatabase($"superchat-extracted-items-{Guid.NewGuid():N}")
            .Options;

        var factory = new TestDbContextFactory(dbContextOptions);
        await using var dbContext = await factory.CreateDbContextAsync(cancellationToken);
        await dbContext.Database.EnsureCreatedAsync(cancellationToken);
        return factory;
    }

    private static ExtractedItemService CreateService(IDbContextFactory<SuperChatDbContext> factory)
    {
        return new ExtractedItemService(
            new ExtractedItemIngestionService(factory, CreateMeetingService(factory)),
            new ExtractedItemQueryService(factory, new ExtractedItemAutoResolutionService(factory)),
            new ExtractedItemManualResolutionService(factory));
    }

    private static MeetingService CreateMeetingService(IDbContextFactory<SuperChatDbContext> factory)
    {
        return new MeetingService(
            new MeetingUpsertService(factory),
            new MeetingUpcomingQueryService(factory, new MeetingAutoResolutionService(factory)),
            new MeetingManualResolutionService(factory));
    }

    private static WorkItemActionService CreateActionService(IDbContextFactory<SuperChatDbContext> factory)
    {
        var extractedItemService = CreateService(factory);
        var meetingService = CreateMeetingService(factory);

        return new WorkItemActionService(
        [
            new RequestWorkItemTypeStrategy(extractedItemService, new ExtractedItemLookupService(factory)),
            new EventWorkItemTypeStrategy(
                extractedItemService,
                meetingService,
                new ExtractedItemLookupService(factory),
                new MeetingLookupService(factory)),
            new ActionItemWorkItemTypeStrategy(extractedItemService, new ExtractedItemLookupService(factory))
        ]);
    }

    private static PilotOptions CreatePilotOptions()
    {
        return new PilotOptions
        {
            TodayTimeZoneId = "Europe/Moscow"
        };
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
