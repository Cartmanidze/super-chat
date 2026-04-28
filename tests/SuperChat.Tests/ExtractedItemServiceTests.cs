using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SuperChat.Contracts.Features.Auth;
using SuperChat.Contracts.Features.Intelligence.Meetings;
using SuperChat.Contracts.Features.Operations;
using SuperChat.Domain.Features.Intelligence;
using SuperChat.Application.Features.WorkItems;
using SuperChat.Infrastructure.Features.Intelligence.Meetings;
using SuperChat.Infrastructure.Features.Intelligence.WorkItems;
using SuperChat.Infrastructure.Features.Operations;
using SuperChat.Infrastructure.Shared.Persistence;
using SuperChat.Infrastructure.Shared.Presentation;

namespace SuperChat.Tests;

public sealed class ExtractedItemServiceTests
{
    [Fact]
    public async Task AddRangeAsync_DoesNotPersistGenericFollowUpCandidate()
    {
        var factory = await CreateFactoryAsync(CancellationToken.None);
        var userId = Guid.NewGuid();
        var service = CreateService(factory);

        await service.AcceptRangeAsync(
        [
            new ExtractedItem(
                Guid.NewGuid(),
                userId,
                ExtractedItemKind.Meeting,
                "Follow-up candidate",
                "video.mp4",
                "!room:matrix.localhost",
                "$evt-1",
                null,
                DateTimeOffset.UtcNow,
                null,
                new Confidence(0.51))
        ],
        CancellationToken.None);

        await using var dbContext = await factory.CreateDbContextAsync(CancellationToken.None);
        var count = await dbContext.WorkItems.CountAsync(CancellationToken.None);

        Assert.Equal(0, count);
    }

    [Fact]
    public async Task GetForUserAsync_FiltersLegacyFollowUpCandidateItems()
    {
        var factory = await CreateFactoryAsync(CancellationToken.None);
        var userId = Guid.NewGuid();

        await using (var dbContext = await factory.CreateDbContextAsync(CancellationToken.None))
        {
            dbContext.WorkItems.AddRange(
            [
                new WorkItemEntity
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    Kind = ExtractedItemKind.Meeting,
                    Title = "Follow-up candidate",
                    Summary = "Обманул",
                    ExternalChatId = "!room:matrix.localhost",
                    SourceEventId = "$evt-legacy",
                    ObservedAt = DateTimeOffset.UtcNow,
                    Confidence = 0.51
                },
                new WorkItemEntity
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    Kind = ExtractedItemKind.Meeting,
                    Title = "Send contract",
                    Summary = "Please send the contract tomorrow.",
                    ExternalChatId = "!sales:matrix.localhost",
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

        await service.AcceptRangeAsync(
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
                new Confidence(0.86))
        ],
        CancellationToken.None);

        await using var dbContext = await factory.CreateDbContextAsync(CancellationToken.None);
        var projectedMeeting = await dbContext.Meetings.SingleAsync(CancellationToken.None);

        Assert.Equal(userId, projectedMeeting.UserId);
        Assert.Equal("$evt-meeting", projectedMeeting.SourceEventId);
        Assert.Equal(dueAt.ToUniversalTime(), projectedMeeting.ScheduledFor);
        Assert.NotNull(projectedMeeting.ScheduledFor);
        Assert.Equal(TimeSpan.Zero, projectedMeeting.ScheduledFor.Value.Offset);
        Assert.Equal(MeetingStatus.PendingConfirmation, projectedMeeting.Status);
        Assert.Equal("Мб заехать за тобой в 11?", projectedMeeting.Summary);

        var meetings = await CreateMeetingService(factory).GetUpcomingAsync(userId, dueAt.AddHours(-2), 10, CancellationToken.None);
        Assert.Single(meetings);
        Assert.Equal(dueAt.ToUniversalTime(), meetings[0].ScheduledFor);
        Assert.Equal(MeetingStatus.PendingConfirmation, meetings[0].Status);
    }

    [Fact]
    public async Task AddRangeAsync_ProjectsMeetingJoinLinkIntoSeparateTable()
    {
        var factory = await CreateFactoryAsync(CancellationToken.None);
        var userId = Guid.NewGuid();
        var service = CreateService(factory);
        var dueAt = new DateTimeOffset(2026, 03, 13, 11, 00, 00, TimeSpan.FromHours(6));
        var joinUrl = new Uri("https://meet.google.com/abc-defg-hij");

        await service.AcceptRangeAsync(
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
                new Confidence(0.93))
        ],
        CancellationToken.None);

        await using var dbContext = await factory.CreateDbContextAsync(CancellationToken.None);
        var projectedMeeting = await dbContext.Meetings.SingleAsync(CancellationToken.None);

        Assert.Equal("GoogleMeet", projectedMeeting.MeetingProvider);
        Assert.Equal(joinUrl.ToString(), projectedMeeting.MeetingJoinUrl);
        Assert.Equal(MeetingStatus.PendingConfirmation, projectedMeeting.Status);

        var meetings = await CreateMeetingService(factory).GetUpcomingAsync(userId, dueAt.AddHours(-2), 10, CancellationToken.None);
        var meeting = Assert.Single(meetings);
        Assert.Equal("GoogleMeet", meeting.MeetingProvider);
        Assert.Equal(joinUrl, meeting.MeetingJoinUrl);
        Assert.Equal(MeetingStatus.PendingConfirmation, meeting.Status);
    }

    [Fact]
    public async Task AddRangeAsync_ProjectsMeetingJoinLinkFromSourceMessageWhenSummaryIsShortened()
    {
        var factory = await CreateFactoryAsync(CancellationToken.None);
        var userId = Guid.NewGuid();
        var service = CreateService(factory);
        var dueAt = new DateTimeOffset(2026, 04, 06, 18, 00, 00, TimeSpan.FromHours(6));
        var joinUrl = new Uri("https://telemost.yandex.ru/j/77013557661694");

        await using (var dbContext = await factory.CreateDbContextAsync(CancellationToken.None))
        {
            dbContext.ChatMessages.Add(new ChatMessageEntity
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Source = "telegram",
                ExternalChatId = "!interview:matrix.localhost",
                ExternalMessageId = "$evt-telemost",
                SenderName = "Stas",
                Text = """
                    напоминаю про сегоднешнее собеседование в 18:00, ссылка на яндекс теелмост

                    https://telemost.yandex.ru/j/77013557661694

                    Звонок в Яндекс Телемосте

                    По ссылке вы сможете подключиться к звонку
                    """,
                SentAt = dueAt.AddHours(-5),
                ReceivedAt = dueAt.AddHours(-5),
                Processed = true
            });

            await dbContext.SaveChangesAsync(CancellationToken.None);
        }

        await service.AcceptRangeAsync(
        [
            new ExtractedItem(
                Guid.NewGuid(),
                userId,
                ExtractedItemKind.Meeting,
                "Upcoming meeting",
                "напоминаю про сегоднешнее собеседование в 18:00, ссылка на яндекс теелмост",
                "!interview:matrix.localhost",
                "$evt-telemost",
                null,
                dueAt.AddHours(-5),
                dueAt,
                new Confidence(0.83))
        ],
        CancellationToken.None);

        await using var verificationContext = await factory.CreateDbContextAsync(CancellationToken.None);
        var meeting = await verificationContext.Meetings.SingleAsync(CancellationToken.None);

        Assert.Equal("YandexTelemost", meeting.MeetingProvider);
        Assert.Equal(joinUrl.ToString(), meeting.MeetingJoinUrl);
    }

    [Fact]
    public async Task GetActiveForUserAsync_DoesNotAutoResolveWaitingOnWhenUserReplies()
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
                Kind = ExtractedItemKind.Meeting,
                Title = "Need to reply",
                Summary = "Marina is waiting for the answer.",
                ExternalChatId = "!sales:matrix.localhost",
                SourceEventId = "$evt-waiting",
                Person = "Marina",
                ObservedAt = observedAt,
                Confidence = 0.91
            });

            dbContext.ChatMessages.Add(new ChatMessageEntity
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
        var entity = await verificationContext.WorkItems.SingleAsync(item => item.Id == itemId, CancellationToken.None);
        Assert.Null(entity.ResolvedAt);
        Assert.Null(entity.ResolutionKind);
        Assert.Null(entity.ResolutionSource);
    }

    [Fact]
    public async Task GetActiveForUserAsync_DoesNotAutoResolveCommitmentWhenCompletionMessageAppears()
    {
        var factory = await CreateFactoryAsync(CancellationToken.None);
        var userId = Guid.NewGuid();
        var observedAt = new DateTimeOffset(2026, 03, 16, 09, 00, 00, TimeSpan.Zero);
        var itemId = Guid.NewGuid();

        await using (var dbContext = await factory.CreateDbContextAsync(CancellationToken.None))
        {
            dbContext.WorkItems.Add(new WorkItemEntity
            {
                Id = itemId,
                UserId = userId,
                Kind = ExtractedItemKind.Meeting,
                Title = "You promised to send the deck",
                Summary = "Need to send the final deck.",
                ExternalChatId = "!sales:matrix.localhost",
                SourceEventId = "$evt-commitment",
                ObservedAt = observedAt,
                Confidence = 0.88
            });

            dbContext.ChatMessages.Add(new ChatMessageEntity
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Source = "telegram",
                ExternalChatId = "!sales:matrix.localhost",
                ExternalMessageId = "$evt-done",
                SenderName = "You",
                Text = "готово, отправил финальный дек",
                SentAt = observedAt.AddMinutes(7),
                ReceivedAt = observedAt.AddMinutes(7),
                Processed = true
            });

            await dbContext.SaveChangesAsync(CancellationToken.None);
        }

        var service = CreateService(factory);
        var items = await service.GetActiveForUserAsync(userId, CancellationToken.None);

        var item = Assert.Single(items);
        Assert.Equal(itemId, item.Id);

        await using var verificationContext = await factory.CreateDbContextAsync(CancellationToken.None);
        var entity = await verificationContext.WorkItems.SingleAsync(item => item.Id == itemId, CancellationToken.None);
        Assert.Null(entity.ResolvedAt);
        Assert.Null(entity.ResolutionKind);
        Assert.Null(entity.ResolutionSource);
    }

    [Fact]
    public async Task GetUpcomingAsync_DoesNotMutateMeetingStateOnRead()
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
                ExternalChatId = "!team:matrix.localhost",
                SourceEventId = "$evt-meeting-finished",
                ObservedAt = scheduledFor.AddHours(-1),
                ScheduledFor = scheduledFor,
                Confidence = 0.93,
                CreatedAt = scheduledFor.AddHours(-1),
                UpdatedAt = scheduledFor.AddHours(-1)
            });

            dbContext.ChatMessages.Add(new ChatMessageEntity
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Source = "telegram",
                ExternalChatId = "!team:matrix.localhost",
                ExternalMessageId = "$evt-after-call",
                SenderName = "Alex",
                Text = "Thanks for the call, I will follow up with notes.",
                SentAt = scheduledFor.AddMinutes(15),
                ReceivedAt = scheduledFor.AddMinutes(15),
                Processed = true
            });

            await dbContext.SaveChangesAsync(CancellationToken.None);
        }

        var meetings = await CreateMeetingService(factory).GetUpcomingAsync(
            userId,
            scheduledFor.AddHours(-2),
            10,
            CancellationToken.None);

        var meeting = Assert.Single(meetings);
        Assert.Equal(meetingId, meeting.Id);

        await using var verificationContext = await factory.CreateDbContextAsync(CancellationToken.None);
        var entity = await verificationContext.Meetings.SingleAsync(item => item.Id == meetingId, CancellationToken.None);
        Assert.Null(entity.ResolvedAt);
        Assert.Null(entity.ResolutionKind);
        Assert.Null(entity.ResolutionSource);
    }

    [Fact]
    public async Task ProjectConversationMeetingsCommandHandler_AutoResolvesProjectedMeetingsAfterProjection()
    {
        var factory = await CreateFactoryAsync(CancellationToken.None);
        var userId = Guid.NewGuid();
        var roomId = "!team:matrix.localhost";
        var chunkObservedAt = new DateTimeOffset(2026, 03, 16, 10, 15, 00, TimeSpan.Zero);
        var handlerNow = new DateTimeOffset(2026, 03, 16, 12, 30, 00, TimeSpan.Zero);

        await using (var dbContext = await factory.CreateDbContextAsync(CancellationToken.None))
        {
            dbContext.MessageChunks.Add(new MessageChunkEntity
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Source = "telegram",
                Provider = "telegram",
                Transport = "matrix_bridge",
                ChatId = roomId,
                Kind = "dialog_chunk",
                Text = """
                    Alex: let's confirm
                    You: итого, встреча сегодня в 12:00, подтверждаю
                    Alex: ok
                    """,
                MessageCount = 3,
                TsFrom = chunkObservedAt.AddMinutes(-15),
                TsTo = chunkObservedAt,
                ContentHash = "chunk-hash-meeting-auto-resolve",
                ChunkVersion = 1,
                CreatedAt = chunkObservedAt,
                UpdatedAt = chunkObservedAt
            });

            dbContext.ChatMessages.Add(new ChatMessageEntity
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Source = "telegram",
                ExternalChatId = roomId,
                ExternalMessageId = "$evt-after-call",
                SenderName = "Alex",
                Text = "Thanks for the call, I will follow up with notes.",
                SentAt = handlerNow.AddMinutes(-5),
                ReceivedAt = handlerNow.AddMinutes(-5),
                Processed = true
            });

            await dbContext.SaveChangesAsync(CancellationToken.None);
        }

        var handler = new ProjectConversationMeetingsCommandHandler(
            new MeetingProjectionService(
                factory,
                Options.Create(new MeetingProjectionOptions
                {
                    Enabled = true
                }),
                CreatePilotOptions(),
                new FixedTimeProvider(handlerNow)),
            new MeetingAutoResolutionService(factory, NullLogger<MeetingAutoResolutionService>.Instance),
            Options.Create(new MeetingProjectionOptions
            {
                Enabled = true
            }),
            new FixedTimeProvider(handlerNow),
            new TestHostApplicationLifetime(),
            NullLogger<ProjectConversationMeetingsCommandHandler>.Instance);

        await handler.Handle(new ProjectConversationMeetingsCommand(userId, roomId));

        await using var verificationContext = await factory.CreateDbContextAsync(CancellationToken.None);
        var entity = await verificationContext.Meetings.SingleAsync(CancellationToken.None);
        Assert.NotNull(entity.ResolvedAt);
        Assert.Equal(WorkItemResolutionState.Completed, entity.ResolutionKind);
        Assert.Equal(WorkItemResolutionState.AutoMeetingCompletion, entity.ResolutionSource);
    }

    [Fact]
    public async Task MeetingWorkItemCommandService_DismissAsync_ResolvesMeetingById()
    {
        var factory = await CreateFactoryAsync(CancellationToken.None);
        var userId = Guid.NewGuid();
        var sourceEventId = "$evt-shared-meeting";
        var meetingId = Guid.NewGuid();
        var scheduledFor = new DateTimeOffset(2026, 03, 17, 10, 00, 00, TimeSpan.Zero);

        await using (var dbContext = await factory.CreateDbContextAsync(CancellationToken.None))
        {
            dbContext.Meetings.Add(new MeetingEntity
            {
                Id = meetingId,
                UserId = userId,
                Title = "Intro call",
                Summary = "Call with product team",
                ExternalChatId = "!team:matrix.localhost",
                SourceEventId = sourceEventId,
                ObservedAt = scheduledFor.AddHours(-2),
                ScheduledFor = scheduledFor,
                Confidence = 0.77,
                CreatedAt = scheduledFor.AddHours(-2),
                UpdatedAt = scheduledFor.AddHours(-2)
            });

            await dbContext.SaveChangesAsync(CancellationToken.None);
        }

        var commandService = CreateMeetingCommandService(factory);
        var result = await commandService.DismissAsync(
            userId,
            meetingId,
            CancellationToken.None);

        Assert.True(result);

        await using var verificationContext = await factory.CreateDbContextAsync(CancellationToken.None);
        var meeting = await verificationContext.Meetings.SingleAsync(item => item.Id == meetingId, CancellationToken.None);

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
        Assert.Equal(MeetingStatus.Confirmed, meeting.Status);
    }

    [Fact]
    public async Task GetUpcomingAsync_DeduplicatesMeetingsByConfidenceWithoutThrowing()
    {
        var factory = await CreateFactoryAsync(CancellationToken.None);
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
                    ExternalChatId = "!team:matrix.localhost",
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
                    ExternalChatId = "!team:matrix.localhost",
                    SourceEventId = "$meeting-high",
                    ObservedAt = scheduledFor.AddHours(-2),
                    ScheduledFor = scheduledFor,
                    Confidence = 0.93,
                    CreatedAt = scheduledFor.AddHours(-2),
                    UpdatedAt = scheduledFor.AddHours(-2)
                }
            ]);

            await dbContext.SaveChangesAsync(CancellationToken.None);
        }

        var meetings = await CreateMeetingService(factory).GetUpcomingAsync(
            userId,
            scheduledFor.AddHours(-4),
            10,
            CancellationToken.None);

        var meeting = Assert.Single(meetings);
        Assert.Equal("Higher confidence duplicate", meeting.Title);
        Assert.Equal("$meeting-high", meeting.SourceEventId);
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

    private static WorkItemService CreateService(IDbContextFactory<SuperChatDbContext> factory)
    {
        return new WorkItemService(
            new WorkItemWriter(CreateMeetingService(factory), NullLogger<WorkItemWriter>.Instance),
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

    private static MeetingWorkItemCommandAppService CreateMeetingCommandService(IDbContextFactory<SuperChatDbContext> factory)
    {
        return new MeetingWorkItemCommandAppService(
            new EfMeetingRepository(factory),
            TimeProvider.System);
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

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow()
        {
            return utcNow;
        }
    }

    private sealed class TestHostApplicationLifetime : IHostApplicationLifetime
    {
        public CancellationToken ApplicationStarted => CancellationToken.None;

        public CancellationToken ApplicationStopping => CancellationToken.None;

        public CancellationToken ApplicationStopped => CancellationToken.None;

        public void StopApplication()
        {
        }
    }
}
