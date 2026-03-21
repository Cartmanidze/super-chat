using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Rebus.Bus;
using Rebus.Bus.Advanced;
using Rebus.Messages;
using SuperChat.Contracts.Features.Operations;
using SuperChat.Domain.Features.Intelligence;
using SuperChat.Domain.Features.Messaging;
using SuperChat.Infrastructure.Abstractions;
using SuperChat.Infrastructure.Features.Intelligence.Extraction;
using SuperChat.Infrastructure.Features.Messaging;
using SuperChat.Infrastructure.Features.Operations;

namespace SuperChat.Tests;

public sealed class ProcessConversationAfterSettleCommandHandlerTests
{
    [Fact]
    public async Task Handle_ProcessesReadyConversationAndMarksMessages()
    {
        var userId = Guid.NewGuid();
        var roomId = "!room:matrix.localhost";
        var message = new NormalizedMessage(
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

        var normalizationService = new RecordingMessageNormalizationService([message]);
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
                0.92)
        ]);
        var workItemService = new RecordingWorkItemService();
        var bus = new RecordingBus();

        var handler = new ProcessConversationAfterSettleCommandHandler(
            normalizationService,
            extractionService,
            workItemService,
            bus,
            Options.Create(new ResolutionOptions()),
            new FixedTimeProvider(message.IngestedAt.AddMinutes(5)),
            NullLogger<ProcessConversationAfterSettleCommandHandler>.Instance);

        await handler.Handle(new ProcessConversationAfterSettleCommand(userId, "telegram", roomId));

        Assert.Equal([message.Id], normalizationService.MarkedProcessedIds);
        Assert.Single(workItemService.IngestedItems);
        Assert.Single(workItemService.IngestedItems[0]);
        Assert.Contains(bus.SentMessages, item => item is ResolveConversationItemsCommand resolve &&
                                                  resolve.UserId == userId &&
                                                  resolve.MatrixRoomId == roomId);
    }

    private sealed class RecordingMessageNormalizationService(IReadOnlyList<NormalizedMessage> pendingMessages) : IMessageNormalizationService
    {
        public List<Guid> MarkedProcessedIds { get; } = [];

        public Task<bool> TryStoreAsync(
            Guid userId,
            string roomId,
            string eventId,
            string senderName,
            string text,
            DateTimeOffset sentAt,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<IReadOnlyList<NormalizedMessage>> GetPendingMessagesAsync(CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<IReadOnlyList<NormalizedMessage>> GetPendingMessagesForConversationAsync(
            Guid userId,
            string source,
            string matrixRoomId,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(
                pendingMessages
                    .Where(item => item.UserId == userId &&
                                   item.Source == source &&
                                   item.MatrixRoomId == matrixRoomId)
                    .ToList() as IReadOnlyList<NormalizedMessage>);
        }

        public Task<IReadOnlyList<NormalizedMessage>> GetRecentMessagesAsync(Guid userId, int take, CancellationToken cancellationToken)
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
        public List<IReadOnlyList<ExtractedItem>> IngestedItems { get; } = [];

        public Task IngestRangeAsync(IEnumerable<ExtractedItem> items, CancellationToken cancellationToken)
        {
            IngestedItems.Add(items.ToList());
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
}
