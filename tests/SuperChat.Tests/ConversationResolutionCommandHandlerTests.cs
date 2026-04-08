using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SuperChat.Contracts.Features.Operations;
using SuperChat.Domain.Features.Intelligence;
using SuperChat.Infrastructure.Features.Intelligence.Meetings;
using SuperChat.Infrastructure.Features.Intelligence.Resolution;
using SuperChat.Infrastructure.Features.Intelligence.WorkItems;
using SuperChat.Infrastructure.Features.Operations;
using SuperChat.Infrastructure.Shared.Persistence;
using SuperChat.Infrastructure.Shared.Presentation;

namespace SuperChat.Tests;

public sealed class ConversationResolutionCommandHandlerTests
{
    [Fact]
    public async Task Handle_ResolvesConversationWorkItemsWithoutReadPath()
    {
        var factory = await CreateFactoryAsync(CancellationToken.None);
        var userId = Guid.NewGuid();
        var observedAt = new DateTimeOffset(2026, 03, 16, 09, 00, 00, TimeSpan.Zero);
        var itemId = Guid.NewGuid();
        var roomId = "!sales:matrix.localhost";

        await using (var dbContext = await factory.CreateDbContextAsync(CancellationToken.None))
        {
            dbContext.WorkItems.Add(new WorkItemEntity
            {
                Id = itemId,
                UserId = userId,
                Kind = ExtractedItemKind.Commitment,
                Title = "You promised to send the deck",
                Summary = "Need to send the final deck.",
                SourceRoom = roomId,
                SourceEventId = "$evt-commitment",
                ObservedAt = observedAt,
                Confidence = 0.88,
                CreatedAt = observedAt,
                UpdatedAt = observedAt
            });

            dbContext.NormalizedMessages.Add(new NormalizedMessageEntity
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Source = "telegram",
                MatrixRoomId = roomId,
                MatrixEventId = "$evt-done",
                SenderName = "You",
                Text = "готово, отправил финальный дек",
                SentAt = observedAt.AddMinutes(7),
                IngestedAt = observedAt.AddMinutes(7),
                Processed = true
            });

            await dbContext.SaveChangesAsync(CancellationToken.None);
        }

        var handler = new ResolveConversationItemsCommandHandler(
            new ConversationResolutionService(
                factory,
                new NoOpAiResolutionService(),
                CreateWorkItemAutoResolutionService(factory, observedAt.AddMinutes(10)),
                new MeetingAutoResolutionService(factory, NullLogger<MeetingAutoResolutionService>.Instance),
                Options.Create(new ResolutionOptions
                {
                    UseLlm = false
                }),
                NullLogger<ConversationResolutionService>.Instance),
            new FixedTimeProvider(observedAt.AddMinutes(10)),
            new TestHostApplicationLifetime(),
            NullLogger<ResolveConversationItemsCommandHandler>.Instance);

        await handler.Handle(new ResolveConversationItemsCommand(userId, roomId));

        await using var verificationContext = await factory.CreateDbContextAsync(CancellationToken.None);
        var entity = await verificationContext.WorkItems.SingleAsync(item => item.Id == itemId, CancellationToken.None);
        Assert.NotNull(entity.ResolvedAt);
        Assert.Equal(WorkItemResolutionState.Completed, entity.ResolutionKind);
        Assert.Equal(WorkItemResolutionState.AutoCompletion, entity.ResolutionSource);
    }

    [Fact]
    public async Task Handle_PersistsAiResolutionTrace()
    {
        var factory = await CreateFactoryAsync(CancellationToken.None);
        var userId = Guid.NewGuid();
        var observedAt = new DateTimeOffset(2026, 03, 16, 09, 00, 00, TimeSpan.Zero);
        var itemId = Guid.NewGuid();
        var roomId = "!sales:matrix.localhost";

        await using (var dbContext = await factory.CreateDbContextAsync(CancellationToken.None))
        {
            dbContext.WorkItems.Add(new WorkItemEntity
            {
                Id = itemId,
                UserId = userId,
                Kind = ExtractedItemKind.Commitment,
                Title = "Send the deck",
                Summary = "Need to send the final deck.",
                SourceRoom = roomId,
                SourceEventId = "$evt-commitment",
                ObservedAt = observedAt,
                Confidence = 0.88,
                CreatedAt = observedAt,
                UpdatedAt = observedAt
            });

            dbContext.NormalizedMessages.Add(new NormalizedMessageEntity
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Source = "telegram",
                MatrixRoomId = roomId,
                MatrixEventId = "$evt-done",
                SenderName = "You",
                Text = "готово, отправил финальный дек",
                SentAt = observedAt.AddMinutes(7),
                IngestedAt = observedAt.AddMinutes(7),
                Processed = true
            });

            await dbContext.SaveChangesAsync(CancellationToken.None);
        }

        var handler = new ResolveConversationItemsCommandHandler(
            new ConversationResolutionService(
                factory,
                new StubAiResolutionService(
                [
                    new AiResolutionDecisionResult(
                        itemId,
                        WorkItemResolutionState.Completed,
                        WorkItemResolutionState.AutoAiCompletion,
                        observedAt.AddMinutes(7),
                        0.93,
                        "deepseek-reasoner",
                        ["$evt-done"])
                ]),
                CreateWorkItemAutoResolutionService(factory, observedAt.AddMinutes(10)),
                new MeetingAutoResolutionService(factory, NullLogger<MeetingAutoResolutionService>.Instance),
                Options.Create(new ResolutionOptions
                {
                    UseLlm = true
                }),
                NullLogger<ConversationResolutionService>.Instance),
            new FixedTimeProvider(observedAt.AddMinutes(10)),
            new TestHostApplicationLifetime(),
            NullLogger<ResolveConversationItemsCommandHandler>.Instance);

        await handler.Handle(new ResolveConversationItemsCommand(userId, roomId));

        await using var verificationContext = await factory.CreateDbContextAsync(CancellationToken.None);
        var entity = await verificationContext.WorkItems.SingleAsync(item => item.Id == itemId, CancellationToken.None);
        Assert.Equal(WorkItemResolutionState.AutoAiCompletion, entity.ResolutionSource);
        Assert.Equal(0.93, entity.ResolutionConfidence);
        Assert.Equal("deepseek-reasoner", entity.ResolutionModel);
        Assert.Equal("[\"$evt-done\"]", entity.ResolutionEvidenceJson);
    }

    [Fact]
    public async Task Handle_DoesNotThrowForWaitingOnMessageWithoutEventId()
    {
        var factory = await CreateFactoryAsync(CancellationToken.None);
        var userId = Guid.NewGuid();
        var observedAt = new DateTimeOffset(2026, 03, 16, 09, 00, 00, TimeSpan.Zero);
        var itemId = Guid.NewGuid();
        var roomId = "!sales:matrix.localhost";

        await using (var dbContext = await factory.CreateDbContextAsync(CancellationToken.None))
        {
            dbContext.WorkItems.Add(new WorkItemEntity
            {
                Id = itemId,
                UserId = userId,
                Kind = ExtractedItemKind.WaitingOn,
                Title = "Жду ответ по смете",
                Summary = "Нужно дождаться финального ответа.",
                SourceRoom = roomId,
                SourceEventId = "$evt-waiting-on",
                ObservedAt = observedAt,
                Confidence = 0.88,
                CreatedAt = observedAt,
                UpdatedAt = observedAt
            });

            dbContext.NormalizedMessages.Add(new NormalizedMessageEntity
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Source = "telegram",
                MatrixRoomId = roomId,
                MatrixEventId = string.Empty,
                SenderName = "You",
                Text = "готово, уже ответил и отправил детали",
                SentAt = observedAt.AddMinutes(7),
                IngestedAt = observedAt.AddMinutes(7),
                Processed = true
            });

            await dbContext.SaveChangesAsync(CancellationToken.None);
        }

        var handler = new ResolveConversationItemsCommandHandler(
            new ConversationResolutionService(
                factory,
                new NoOpAiResolutionService(),
                CreateWorkItemAutoResolutionService(factory, observedAt.AddMinutes(10)),
                new MeetingAutoResolutionService(factory, NullLogger<MeetingAutoResolutionService>.Instance),
                Options.Create(new ResolutionOptions
                {
                    UseLlm = false
                }),
                NullLogger<ConversationResolutionService>.Instance),
            new FixedTimeProvider(observedAt.AddMinutes(10)),
            new TestHostApplicationLifetime(),
            NullLogger<ResolveConversationItemsCommandHandler>.Instance);

        var exception = await Record.ExceptionAsync(() => handler.Handle(new ResolveConversationItemsCommand(userId, roomId)));

        Assert.Null(exception);
        await using var verificationContext = await factory.CreateDbContextAsync(CancellationToken.None);
        var entity = await verificationContext.WorkItems.SingleAsync(item => item.Id == itemId, CancellationToken.None);
        Assert.NotNull(entity.ResolvedAt);
        Assert.Equal(WorkItemResolutionState.Completed, entity.ResolutionKind);
        Assert.Equal(WorkItemResolutionState.AutoReply, entity.ResolutionSource);
    }

    [Fact]
    public async Task Handle_DoesNotApplyAiRescheduledDecisionToFutureMeeting()
    {
        var factory = await CreateFactoryAsync(CancellationToken.None);
        var userId = Guid.NewGuid();
        var roomId = "!meetings:matrix.localhost";
        var now = new DateTimeOffset(2026, 04, 08, 09, 00, 00, TimeSpan.Zero);
        var meetingId = Guid.NewGuid();
        var scheduledFor = now.AddHours(2);

        await using (var dbContext = await factory.CreateDbContextAsync(CancellationToken.None))
        {
            dbContext.Meetings.Add(new MeetingEntity
            {
                Id = meetingId,
                UserId = userId,
                Title = "Demo",
                Summary = "созвон сегодня в 14:00",
                SourceRoom = roomId,
                SourceEventId = "$evt-meeting",
                ObservedAt = now.AddMinutes(-10),
                ScheduledFor = scheduledFor,
                Confidence = 0.91,
                CreatedAt = now.AddMinutes(-10),
                UpdatedAt = now.AddMinutes(-10)
            });

            dbContext.NormalizedMessages.Add(new NormalizedMessageEntity
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Source = "telegram",
                MatrixRoomId = roomId,
                MatrixEventId = "$evt-later",
                SenderName = "Alex",
                Text = "давай перенесем на попозже",
                SentAt = now.AddMinutes(5),
                IngestedAt = now.AddMinutes(5),
                Processed = true
            });

            await dbContext.SaveChangesAsync(CancellationToken.None);
        }

        var aiService = new StubAiResolutionService(
        [
            new AiResolutionDecisionResult(
                meetingId,
                WorkItemResolutionState.Rescheduled,
                WorkItemResolutionState.AutoAiMeetingCompletion,
                scheduledFor.AddMinutes(5),
                0.95,
                "deepseek-reasoner",
                ["$evt-later"])
        ]);

        var handler = CreateHandler(factory, aiService, now, useLlm: true);
        await handler.Handle(new ResolveConversationItemsCommand(userId, roomId));

        await using var verificationContext = await factory.CreateDbContextAsync(CancellationToken.None);
        var entity = await verificationContext.Meetings.SingleAsync(item => item.Id == meetingId, CancellationToken.None);
        Assert.Null(entity.ResolvedAt);
        Assert.Null(entity.ResolutionKind);
    }

    [Fact]
    public async Task Handle_AppliesAiCancelledDecisionToFutureMeeting()
    {
        var factory = await CreateFactoryAsync(CancellationToken.None);
        var userId = Guid.NewGuid();
        var roomId = "!meetings:matrix.localhost";
        var now = new DateTimeOffset(2026, 04, 08, 09, 00, 00, TimeSpan.Zero);
        var meetingId = Guid.NewGuid();
        var scheduledFor = now.AddHours(2);

        await using (var dbContext = await factory.CreateDbContextAsync(CancellationToken.None))
        {
            dbContext.Meetings.Add(new MeetingEntity
            {
                Id = meetingId,
                UserId = userId,
                Title = "Demo",
                Summary = "созвон сегодня в 14:00",
                SourceRoom = roomId,
                SourceEventId = "$evt-meeting",
                ObservedAt = now.AddMinutes(-10),
                ScheduledFor = scheduledFor,
                Confidence = 0.91,
                CreatedAt = now.AddMinutes(-10),
                UpdatedAt = now.AddMinutes(-10)
            });

            dbContext.NormalizedMessages.Add(new NormalizedMessageEntity
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Source = "telegram",
                MatrixRoomId = roomId,
                MatrixEventId = "$evt-later",
                SenderName = "Alex",
                Text = "отменяем созвон",
                SentAt = now.AddMinutes(5),
                IngestedAt = now.AddMinutes(5),
                Processed = true
            });

            await dbContext.SaveChangesAsync(CancellationToken.None);
        }

        var aiService = new StubAiResolutionService(
        [
            new AiResolutionDecisionResult(
                meetingId,
                WorkItemResolutionState.Cancelled,
                WorkItemResolutionState.AutoAiMeetingCompletion,
                now.AddMinutes(5),
                0.95,
                "deepseek-reasoner",
                ["$evt-later"])
        ]);

        var handler = CreateHandler(factory, aiService, now, useLlm: true);
        await handler.Handle(new ResolveConversationItemsCommand(userId, roomId));

        await using var verificationContext = await factory.CreateDbContextAsync(CancellationToken.None);
        var entity = await verificationContext.Meetings.SingleAsync(item => item.Id == meetingId, CancellationToken.None);
        Assert.Equal(WorkItemResolutionState.Cancelled, entity.ResolutionKind);
        Assert.NotNull(entity.ResolvedAt);
    }

    [Fact]
    public async Task Handle_DoesNotHeuristicallyResolveFutureMeetingDuringConversationPass()
    {
        var factory = await CreateFactoryAsync(CancellationToken.None);
        var userId = Guid.NewGuid();
        var roomId = "!meetings:matrix.localhost";
        var now = new DateTimeOffset(2026, 04, 08, 09, 00, 00, TimeSpan.Zero);
        var meetingId = Guid.NewGuid();
        var scheduledFor = now.AddHours(2);

        await using (var dbContext = await factory.CreateDbContextAsync(CancellationToken.None))
        {
            dbContext.Meetings.Add(new MeetingEntity
            {
                Id = meetingId,
                UserId = userId,
                Title = "Demo",
                Summary = "созвон сегодня в 14:00",
                SourceRoom = roomId,
                SourceEventId = "$evt-meeting",
                ObservedAt = now.AddMinutes(-10),
                ScheduledFor = scheduledFor,
                Confidence = 0.91,
                CreatedAt = now.AddMinutes(-10),
                UpdatedAt = now.AddMinutes(-10)
            });

            dbContext.NormalizedMessages.Add(new NormalizedMessageEntity
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Source = "telegram",
                MatrixRoomId = roomId,
                MatrixEventId = "$evt-after-call",
                SenderName = "Alex",
                Text = "thanks for the call",
                SentAt = now.AddMinutes(1),
                IngestedAt = now.AddMinutes(1),
                Processed = true
            });

            await dbContext.SaveChangesAsync(CancellationToken.None);
        }

        var handler = CreateHandler(factory, new NoOpAiResolutionService(), now, useLlm: false);
        await handler.Handle(new ResolveConversationItemsCommand(userId, roomId));

        await using var verificationContext = await factory.CreateDbContextAsync(CancellationToken.None);
        var entity = await verificationContext.Meetings.SingleAsync(item => item.Id == meetingId, CancellationToken.None);
        Assert.Null(entity.ResolvedAt);
        Assert.Null(entity.ResolutionKind);
    }

    [Fact]
    public async Task Handle_SkipsYoungWorkItemForAiResolutionCandidates()
    {
        var factory = await CreateFactoryAsync(CancellationToken.None);
        var userId = Guid.NewGuid();
        var roomId = "!sales:matrix.localhost";
        var now = new DateTimeOffset(2026, 04, 08, 09, 00, 00, TimeSpan.Zero);
        var itemId = Guid.NewGuid();

        await using (var dbContext = await factory.CreateDbContextAsync(CancellationToken.None))
        {
            dbContext.WorkItems.Add(new WorkItemEntity
            {
                Id = itemId,
                UserId = userId,
                Kind = ExtractedItemKind.Task,
                Title = "Отправить смету",
                Summary = "Отправить смету клиенту",
                SourceRoom = roomId,
                SourceEventId = "$evt-task",
                ObservedAt = now.AddMinutes(-1),
                Confidence = 0.88,
                CreatedAt = now.AddMinutes(-1),
                UpdatedAt = now.AddMinutes(-1)
            });

            dbContext.NormalizedMessages.Add(new NormalizedMessageEntity
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Source = "telegram",
                MatrixRoomId = roomId,
                MatrixEventId = "$evt-done",
                SenderName = "You",
                Text = "готово, отправил",
                SentAt = now,
                IngestedAt = now,
                Processed = true
            });

            await dbContext.SaveChangesAsync(CancellationToken.None);
        }

        var aiService = new CapturingAiResolutionService();
        var handler = CreateHandler(factory, aiService, now, useLlm: true);
        await handler.Handle(new ResolveConversationItemsCommand(userId, roomId));

        Assert.Empty(aiService.SeenCandidates);
    }

    [Fact]
    public async Task Handle_SkipsYoungWorkItemForHeuristicAutoResolution()
    {
        var factory = await CreateFactoryAsync(CancellationToken.None);
        var userId = Guid.NewGuid();
        var roomId = "!sales:matrix.localhost";
        var now = new DateTimeOffset(2026, 04, 08, 09, 00, 00, TimeSpan.Zero);
        var itemId = Guid.NewGuid();

        await using (var dbContext = await factory.CreateDbContextAsync(CancellationToken.None))
        {
            dbContext.WorkItems.Add(new WorkItemEntity
            {
                Id = itemId,
                UserId = userId,
                Kind = ExtractedItemKind.Task,
                Title = "Отправить смету",
                Summary = "Отправить смету клиенту",
                SourceRoom = roomId,
                SourceEventId = "$evt-task",
                ObservedAt = now.AddMinutes(-1),
                Confidence = 0.88,
                CreatedAt = now.AddMinutes(-1),
                UpdatedAt = now.AddMinutes(-1)
            });

            dbContext.NormalizedMessages.Add(new NormalizedMessageEntity
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Source = "telegram",
                MatrixRoomId = roomId,
                MatrixEventId = "$evt-done",
                SenderName = "You",
                Text = "готово, отправил",
                SentAt = now,
                IngestedAt = now,
                Processed = true
            });

            await dbContext.SaveChangesAsync(CancellationToken.None);
        }

        var handler = CreateHandler(factory, new NoOpAiResolutionService(), now, useLlm: false);
        await handler.Handle(new ResolveConversationItemsCommand(userId, roomId));

        await using var verificationContext = await factory.CreateDbContextAsync(CancellationToken.None);
        var entity = await verificationContext.WorkItems.SingleAsync(item => item.Id == itemId, CancellationToken.None);
        Assert.Null(entity.ResolvedAt);
        Assert.Null(entity.ResolutionKind);
    }

    private static ResolveConversationItemsCommandHandler CreateHandler(
        IDbContextFactory<SuperChatDbContext> factory,
        IAiResolutionService aiResolutionService,
        DateTimeOffset now,
        bool useLlm)
    {
        return new ResolveConversationItemsCommandHandler(
            new ConversationResolutionService(
                factory,
                aiResolutionService,
                CreateWorkItemAutoResolutionService(factory, now),
                new MeetingAutoResolutionService(factory, NullLogger<MeetingAutoResolutionService>.Instance),
                Options.Create(new ResolutionOptions
                {
                    UseLlm = useLlm
                }),
                NullLogger<ConversationResolutionService>.Instance),
            new FixedTimeProvider(now),
            new TestHostApplicationLifetime(),
            NullLogger<ResolveConversationItemsCommandHandler>.Instance);
    }

    private static WorkItemAutoResolutionService CreateWorkItemAutoResolutionService(
        IDbContextFactory<SuperChatDbContext> factory,
        DateTimeOffset now)
    {
        return new WorkItemAutoResolutionService(
            factory,
            NullLogger<WorkItemAutoResolutionService>.Instance,
            new FixedTimeProvider(now),
            Options.Create(new ResolutionOptions()));
    }

    private static async Task<IDbContextFactory<SuperChatDbContext>> CreateFactoryAsync(CancellationToken cancellationToken)
    {
        var dbContextOptions = new DbContextOptionsBuilder<SuperChatDbContext>()
            .UseInMemoryDatabase($"superchat-conversation-resolution-{Guid.NewGuid():N}")
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

    private sealed class NoOpAiResolutionService : IAiResolutionService
    {
        public Task<IReadOnlyList<AiResolutionDecisionResult>> ResolveAsync(
            IReadOnlyList<ConversationResolutionCandidate> candidates,
            CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyList<AiResolutionDecisionResult>>(Array.Empty<AiResolutionDecisionResult>());
        }
    }

    private sealed class StubAiResolutionService(IReadOnlyList<AiResolutionDecisionResult> decisions) : IAiResolutionService
    {
        public Task<IReadOnlyList<AiResolutionDecisionResult>> ResolveAsync(
            IReadOnlyList<ConversationResolutionCandidate> candidates,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(decisions);
        }
    }

    private sealed class CapturingAiResolutionService : IAiResolutionService
    {
        public List<ConversationResolutionCandidate> SeenCandidates { get; } = [];

        public Task<IReadOnlyList<AiResolutionDecisionResult>> ResolveAsync(
            IReadOnlyList<ConversationResolutionCandidate> candidates,
            CancellationToken cancellationToken)
        {
            SeenCandidates.AddRange(candidates);
            return Task.FromResult<IReadOnlyList<AiResolutionDecisionResult>>(Array.Empty<AiResolutionDecisionResult>());
        }
    }
}
