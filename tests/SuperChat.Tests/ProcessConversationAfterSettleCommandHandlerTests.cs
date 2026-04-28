using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Hosting;
using Rebus.Bus;
using Rebus.Bus.Advanced;
using SuperChat.Contracts.Features.Intelligence.Extraction;
using SuperChat.Contracts.Features.Messaging;
using SuperChat.Contracts.Features.Operations;
using SuperChat.Contracts.Features.WorkItems;
using SuperChat.Domain.Features.Intelligence;
using SuperChat.Domain.Features.Messaging;
using SuperChat.Infrastructure.Features.Operations;

namespace SuperChat.Tests;

public sealed class ProcessConversationAfterSettleCommandHandlerTests
{
    [Fact]
    public async Task Handle_ProcessesReadyConversationAndMarksMessages()
    {
        var userId = Guid.NewGuid();
        var roomId = "!room:matrix.localhost";
        var message = new ChatMessage(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            userId,
            "telegram",
            roomId,
            "$evt-1",
            "Alice",
            "Please call me tomorrow.",
            new DateTimeOffset(2026, 03, 13, 10, 00, 00, TimeSpan.Zero),
            new DateTimeOffset(2026, 03, 13, 10, 00, 00, TimeSpan.Zero),
            false);

        var normalizationService = new RecordingChatMessageStore([message]);
        var extractionService = new StubExtractionService(
        [
            new ExtractedItem(
                Guid.Parse("22222222-2222-2222-2222-222222222222"),
                userId,
                ExtractedItemKind.Task,
                "Follow up",
                "Please call me tomorrow.",
                roomId,
                "$evt-1",
                "Alice",
                message.SentAt,
                message.SentAt.AddDays(1),
                new Confidence(0.92))
        ]);
        var workItemService = new RecordingWorkItemService();
        var bus = new RecordingBus();

        var handler = new ProcessConversationAfterSettleCommandHandler(
            normalizationService,
            extractionService,
            workItemService,
            bus,
            Options.Create(new ResolutionOptions()),
            new FixedTimeProvider(message.ReceivedAt.AddMinutes(5)),
            new TestHostApplicationLifetime(),
            NullLogger<ProcessConversationAfterSettleCommandHandler>.Instance);

        await handler.Handle(new ProcessConversationAfterSettleCommand(userId, "telegram", roomId));

        Assert.Equal([message.Id], normalizationService.MarkedProcessedIds);
        Assert.Single(workItemService.AcceptedItems);
        Assert.Single(workItemService.AcceptedItems[0]);
        Assert.Contains(bus.SentMessages, item => item is ResolveConversationItemsCommand resolve &&
                                                  resolve.UserId == userId &&
                                                  resolve.ExternalChatId == roomId);
    }

    [Fact]
    public async Task Handle_WhenConversationWindowIsNotReady_DefersRetryAndKeepsMessagePending()
    {
        var userId = Guid.NewGuid();
        var roomId = "!room:matrix.localhost";
        var message = new ChatMessage(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            userId,
            "telegram",
            roomId,
            "$evt-1",
            "glebov84",
            "переносим на 19:00, ссылка всё та же",
            new DateTimeOffset(2026, 04, 06, 11, 37, 23, TimeSpan.Zero),
            new DateTimeOffset(2026, 04, 06, 11, 37, 26, TimeSpan.Zero),
            false);

        var normalizationService = new RecordingChatMessageStore([message]);
        var extractionService = new StubExtractionService([]);
        var workItemService = new RecordingWorkItemService();
        var bus = new RecordingBus();

        var handler = new ProcessConversationAfterSettleCommandHandler(
            normalizationService,
            extractionService,
            workItemService,
            bus,
            Options.Create(new ResolutionOptions()),
            new FixedTimeProvider(message.ReceivedAt.AddSeconds(19)),
            new TestHostApplicationLifetime(),
            NullLogger<ProcessConversationAfterSettleCommandHandler>.Instance);

        await handler.Handle(new ProcessConversationAfterSettleCommand(userId, "telegram", roomId));

        Assert.Empty(normalizationService.MarkedProcessedIds);
        Assert.Empty(workItemService.AcceptedItems);
        Assert.Empty(bus.SentMessages);

        var retry = Assert.Single(bus.DeferredMessages);
        var retryCommand = Assert.IsType<ProcessConversationAfterSettleCommand>(retry.Message);
        Assert.Equal(userId, retryCommand.UserId);
        Assert.Equal(roomId, retryCommand.ExternalChatId);
        Assert.True(retry.Delay > TimeSpan.Zero);
        Assert.True(retry.Delay <= TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task Handle_WhenExtractionReturnsNoItems_DoesNotScheduleDeferredConversationResolve()
    {
        var userId = Guid.NewGuid();
        var roomId = "!room:matrix.localhost";
        var message = new ChatMessage(
            Guid.Parse("33333333-3333-3333-3333-333333333333"),
            userId,
            "telegram",
            roomId,
            "$evt-empty",
            "Alice",
            "just chatting",
            new DateTimeOffset(2026, 04, 08, 10, 00, 00, TimeSpan.Zero),
            new DateTimeOffset(2026, 04, 08, 10, 00, 00, TimeSpan.Zero),
            false);

        var bus = new RecordingBus();
        var handler = new ProcessConversationAfterSettleCommandHandler(
            new RecordingChatMessageStore([message]),
            new StubExtractionService([]),
            new RecordingWorkItemService(),
            bus,
            Options.Create(new ResolutionOptions
            {
                ScheduleDeferredConversationPass = true
            }),
            new FixedTimeProvider(message.ReceivedAt.AddMinutes(5)),
            new TestHostApplicationLifetime(),
            NullLogger<ProcessConversationAfterSettleCommandHandler>.Instance);

        await handler.Handle(new ProcessConversationAfterSettleCommand(userId, "telegram", roomId));

        Assert.Contains(bus.SentMessages, item => item is ResolveConversationItemsCommand resolve &&
                                                  resolve.UserId == userId &&
                                                  resolve.ExternalChatId == roomId);
        Assert.DoesNotContain(bus.DeferredMessages, item => item.Message is ResolveConversationItemsCommand);
    }

    [Fact]
    public async Task Handle_WhenExtractionReturnsItems_StillSchedulesDeferredConversationResolve()
    {
        var userId = Guid.NewGuid();
        var roomId = "!room:matrix.localhost";
        var message = new ChatMessage(
            Guid.Parse("44444444-4444-4444-4444-444444444444"),
            userId,
            "telegram",
            roomId,
            "$evt-task",
            "Alice",
            "please prepare the notes",
            new DateTimeOffset(2026, 04, 08, 11, 00, 00, TimeSpan.Zero),
            new DateTimeOffset(2026, 04, 08, 11, 00, 00, TimeSpan.Zero),
            false);

        var bus = new RecordingBus();
        var handler = new ProcessConversationAfterSettleCommandHandler(
            new RecordingChatMessageStore([message]),
            new StubExtractionService(
            [
                new ExtractedItem(
                    Guid.Parse("55555555-5555-5555-5555-555555555555"),
                    userId,
                    ExtractedItemKind.Task,
                    "Prepare notes",
                    "please prepare the notes",
                    roomId,
                    "$evt-task",
                    "Alice",
                    message.SentAt,
                    null,
                    new Confidence(0.9))
            ]),
            new RecordingWorkItemService(),
            bus,
            Options.Create(new ResolutionOptions
            {
                ScheduleDeferredConversationPass = true
            }),
            new FixedTimeProvider(message.ReceivedAt.AddMinutes(5)),
            new TestHostApplicationLifetime(),
            NullLogger<ProcessConversationAfterSettleCommandHandler>.Instance);

        await handler.Handle(new ProcessConversationAfterSettleCommand(userId, "telegram", roomId));

        Assert.Contains(bus.SentMessages, item => item is ResolveConversationItemsCommand resolve &&
                                                  resolve.UserId == userId &&
                                                  resolve.ExternalChatId == roomId);
        Assert.Contains(bus.DeferredMessages, item => item.Message is ResolveConversationItemsCommand resolve &&
                                                      resolve.UserId == userId &&
                                                      resolve.ExternalChatId == roomId);
    }

    private sealed class RecordingChatMessageStore(IReadOnlyList<ChatMessage> pendingMessages) : IChatMessageStore
    {
        public List<Guid> MarkedProcessedIds { get; } = [];

        public Task<bool> TryStoreAsync(
            Guid userId,
            string source,
            string externalChatId,
            string externalMessageId,
            string senderName,
            string text,
            DateTimeOffset sentAt,
            CancellationToken cancellationToken,
            string? chatTitle = null)
        {
            throw new NotSupportedException();
        }

        public Task<IReadOnlyList<ChatMessage>> GetPendingMessagesAsync(CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<IReadOnlyList<ChatMessage>> GetPendingMessagesForConversationAsync(
            Guid userId,
            string source,
            string matrixRoomId,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(
                pendingMessages
                    .Where(item => item.UserId == userId &&
                                   item.Source == source &&
                                   item.ExternalChatId == matrixRoomId)
                    .ToList() as IReadOnlyList<ChatMessage>);
        }

        public Task<IReadOnlyList<ChatMessage>> GetRecentMessagesAsync(Guid userId, int take, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<IReadOnlyList<ChatMessage>> SearchRecentMessagesAsync(Guid userId, string query, int limit, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task MarkProcessedAsync(IEnumerable<Guid> messageIds, CancellationToken cancellationToken)
        {
            MarkedProcessedIds.AddRange(messageIds);
            return Task.CompletedTask;
        }
    }

    private sealed class StubExtractionService(IReadOnlyCollection<ExtractedItem> items) : IAiStructuredExtractionService
    {
        public Task<IReadOnlyCollection<ExtractedItem>> ExtractAsync(ConversationWindow window, CancellationToken cancellationToken)
        {
            return Task.FromResult(items);
        }
    }

    private sealed class RecordingWorkItemService : IWorkItemService
    {
        public List<IReadOnlyList<ExtractedItem>> AcceptedItems { get; } = [];

        public Task AcceptRangeAsync(IEnumerable<ExtractedItem> items, CancellationToken cancellationToken)
        {
            AcceptedItems.Add(items.ToList());
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<WorkItemRecord>> GetForUserAsync(Guid userId, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<IReadOnlyList<WorkItemRecord>> GetActiveForUserAsync(Guid userId, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<IReadOnlyList<WorkItemRecord>> SearchAsync(Guid userId, string query, int limit, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<bool> CompleteAsync(Guid userId, Guid workItemId, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<bool> DismissAsync(Guid userId, Guid workItemId, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow()
        {
            return utcNow;
        }
    }

    private sealed class RecordingBus : IBus
    {
        public List<object> SentMessages { get; } = [];
        public List<(TimeSpan Delay, object Message)> DeferredMessages { get; } = [];

        public IAdvancedApi Advanced => throw new NotSupportedException();

        public Task Send(object commandMessage, IDictionary<string, string>? optionalHeaders = null)
        {
            SentMessages.Add(commandMessage);
            return Task.CompletedTask;
        }

        public Task SendLocal(object commandMessage, IDictionary<string, string>? optionalHeaders = null)
        {
            SentMessages.Add(commandMessage);
            return Task.CompletedTask;
        }

        public Task Defer(TimeSpan delay, object message, IDictionary<string, string>? optionalHeaders = null)
        {
            DeferredMessages.Add((delay, message));
            return Task.CompletedTask;
        }

        public Task Defer(DateTimeOffset deferredUntil, object message, IDictionary<string, string>? optionalHeaders = null)
        {
            DeferredMessages.Add((deferredUntil - DateTimeOffset.UtcNow, message));
            return Task.CompletedTask;
        }

        public Task DeferLocal(TimeSpan delay, object message, IDictionary<string, string>? optionalHeaders = null)
        {
            DeferredMessages.Add((delay, message));
            return Task.CompletedTask;
        }

        public Task DeferLocal(DateTimeOffset deferredUntil, object message, IDictionary<string, string>? optionalHeaders = null)
        {
            DeferredMessages.Add((deferredUntil - DateTimeOffset.UtcNow, message));
            return Task.CompletedTask;
        }

        public Task Publish(object eventMessage, IDictionary<string, string>? optionalHeaders = null)
        {
            throw new NotSupportedException();
        }

        public Task Reply(object replyMessage, IDictionary<string, string>? optionalHeaders = null)
        {
            throw new NotSupportedException();
        }

        public Task Subscribe(Type eventType)
        {
            throw new NotSupportedException();
        }

        public Task Subscribe<TEvent>()
        {
            throw new NotSupportedException();
        }

        public Task Subscribe(Type eventType, string topic)
        {
            throw new NotSupportedException();
        }

        public Task Unsubscribe(Type eventType)
        {
            throw new NotSupportedException();
        }

        public Task Unsubscribe<TEvent>()
        {
            throw new NotSupportedException();
        }

        public void Dispose()
        {
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
