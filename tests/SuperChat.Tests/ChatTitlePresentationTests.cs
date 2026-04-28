using Microsoft.Extensions.Logging.Abstractions;
using SuperChat.Contracts.Features.Auth;
using SuperChat.Contracts.Features.Intelligence.Meetings;
using SuperChat.Contracts.Features.Intelligence.Retrieval;
using SuperChat.Contracts.Features.Messaging;
using SuperChat.Contracts.Features.WorkItems;
using SuperChat.Domain.Features.Intelligence;
using SuperChat.Domain.Features.Messaging;
using SuperChat.Infrastructure.Features.Intelligence.Digest;
using SuperChat.Infrastructure.Features.Search;
using DomainMeetingStatus = SuperChat.Domain.Features.Intelligence.MeetingStatus;

namespace SuperChat.Tests;

public sealed class ChatTitlePresentationTests
{
    [Fact]
    public async Task SearchService_ReplacesChatIdWithResolvedTitle()
    {
        var userId = Guid.NewGuid();
        var service = new SearchService(
            new StubWorkItemService([
                new WorkItemRecord(
                    Guid.NewGuid(),
                    userId,
                    ExtractedItemKind.Task,
                    "Send contract",
                    "Please send the contract tomorrow.",
                    "!sales:matrix.localhost",
                    "$evt-1",
                    null,
                    DateTimeOffset.UtcNow,
                    DateTimeOffset.UtcNow.AddHours(1),
                    new Confidence(0.9))
            ]),
            new StubChatMessageStore([]),
            new StubChatTitleService(new Dictionary<string, string>
            {
                ["!sales:matrix.localhost"] = "Sales Team"
            }));

        var results = await service.SearchAsync(userId, "contract", CancellationToken.None);

        Assert.Single(results);
        Assert.Equal("Sales Team", results[0].ChatTitle);
    }

    [Fact]
    public async Task SearchService_ReplacesChatIdForRawMessageFallback()
    {
        var userId = Guid.NewGuid();
        var service = new SearchService(
            new StubWorkItemService([]),
            new StubChatMessageStore([
                new ChatMessage(
                    Guid.NewGuid(),
                    userId,
                    "telegram",
                    "!founders:matrix.localhost",
                    "$evt-2",
                    "Alex",
                    "pilot-marker needs follow-up",
                    DateTimeOffset.UtcNow,
                    DateTimeOffset.UtcNow,
                    false)
            ]),
            new StubChatTitleService(new Dictionary<string, string>
            {
                ["!founders:matrix.localhost"] = "Founders"
            }));

        var results = await service.SearchAsync(userId, "pilot-marker", CancellationToken.None);

        Assert.Single(results);
        Assert.Equal("Founders", results[0].ChatTitle);
    }

    [Fact]
    public async Task SearchService_UsesChatTitleWhenRawMessageSenderIsOpaqueNumericId()
    {
        var userId = Guid.NewGuid();
        var service = new SearchService(
            new StubWorkItemService([]),
            new StubChatMessageStore([
                new ChatMessage(
                    Guid.NewGuid(),
                    userId,
                    "telegram",
                    "!dm:matrix.localhost",
                    "$evt-opaque",
                    "349223531",
                    "video.mp4",
                    DateTimeOffset.UtcNow,
                    DateTimeOffset.UtcNow,
                    false)
            ]),
            new StubChatTitleService(new Dictionary<string, string>
            {
                ["!dm:matrix.localhost"] = "Bi (Telegram)"
            }));

        var results = await service.SearchAsync(userId, "video", CancellationToken.None);

        Assert.Single(results);
        Assert.Equal("Bi", results[0].Title);
        Assert.Equal("Bi (Telegram)", results[0].ChatTitle);
    }

    [Fact]
    public async Task DigestService_ReplacesChatIdWithResolvedTitle()
    {
        var userId = Guid.NewGuid();
        var now = new DateTimeOffset(2026, 03, 12, 12, 00, 00, TimeSpan.Zero);
        var service = new DigestService(
            new StubMeetingService([
                new MeetingRecord(
                    Guid.NewGuid(),
                    userId,
                    "Action needed",
                    "Please send the proposal tomorrow.",
                    "!team:matrix.localhost",
                    "$evt-3",
                    null,
                    now,
                    now.AddHours(2),
                    new Confidence(0.95),
                    null,
                    null,
                    null)
            ]),
            new StubChatTitleService(new Dictionary<string, string>
            {
                ["!team:matrix.localhost"] = "Sales Ops"
            }),
            new FixedTimeProvider(now),
            new PilotOptions
            {
                TodayTimeZoneId = "Europe/Moscow"
            },
            NullLogger<DigestService>.Instance);

        var cards = await service.GetMeetingsAsync(userId, CancellationToken.None);

        Assert.Single(cards);
        Assert.Equal("Sales Ops", cards[0].ChatTitle);
    }

    [Fact]
    public async Task DigestService_UsesPersistedMeetingStatus()
    {
        var userId = Guid.NewGuid();
        var now = new DateTimeOffset(2026, 03, 12, 12, 00, 00, TimeSpan.Zero);
        var service = new DigestService(
            new StubMeetingService([
                new MeetingRecord(
                    Guid.NewGuid(),
                    userId,
                    "Status should come from entity",
                    "Neutral summary without confirmation keywords",
                    "!team:matrix.localhost",
                    "$evt-status",
                    null,
                    now,
                    now.AddHours(2),
                    new Confidence(0.9),
                    Status: DomainMeetingStatus.Confirmed)
            ]),
            new StubChatTitleService(new Dictionary<string, string>()),
            new FixedTimeProvider(now),
            new PilotOptions
            {
                TodayTimeZoneId = "Europe/Moscow"
            },
            NullLogger<DigestService>.Instance);

        var cards = await service.GetMeetingsAsync(userId, CancellationToken.None);

        var card = Assert.Single(cards);
        Assert.Equal(WorkItemStatus.Confirmed, card.Status);
    }

    private sealed class StubMeetingService(IReadOnlyList<MeetingRecord> items) : IMeetingService
    {
        public Task UpsertRangeAsync(IEnumerable<ExtractedItem> items, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<IReadOnlyList<MeetingRecord>> GetUpcomingAsync(Guid userId, DateTimeOffset fromInclusive, int take, CancellationToken cancellationToken)
        {
            return Task.FromResult(items.Where(item => item.UserId == userId).Take(take).ToList() as IReadOnlyList<MeetingRecord>);
        }

        public Task<bool> CompleteAsync(Guid userId, Guid meetingId, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<bool> DismissAsync(Guid userId, Guid meetingId, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class StubWorkItemService(IReadOnlyList<WorkItemRecord> items) : IWorkItemService
    {
        public Task AcceptRangeAsync(IEnumerable<ExtractedItem> items, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<IReadOnlyList<WorkItemRecord>> GetForUserAsync(Guid userId, CancellationToken cancellationToken)
        {
            return Task.FromResult(items.Where(item => item.UserId == userId).ToList() as IReadOnlyList<WorkItemRecord>);
        }

        public Task<IReadOnlyList<WorkItemRecord>> GetActiveForUserAsync(Guid userId, CancellationToken cancellationToken)
        {
            return Task.FromResult(items.Where(item => item.UserId == userId).ToList() as IReadOnlyList<WorkItemRecord>);
        }

        public Task<IReadOnlyList<WorkItemRecord>> SearchAsync(Guid userId, string query, int limit, CancellationToken cancellationToken)
        {
            if (limit <= 0 || string.IsNullOrWhiteSpace(query))
            {
                return Task.FromResult(Array.Empty<WorkItemRecord>() as IReadOnlyList<WorkItemRecord>);
            }

            var trimmed = query.Trim();
            var matches = items
                .Where(item => item.UserId == userId &&
                    (item.Title.Contains(trimmed, StringComparison.OrdinalIgnoreCase) ||
                     item.Summary.Contains(trimmed, StringComparison.OrdinalIgnoreCase) ||
                     item.ExternalChatId.Contains(trimmed, StringComparison.OrdinalIgnoreCase)))
                .OrderByDescending(item => item.ObservedAt)
                .Take(limit)
                .ToList();

            return Task.FromResult(matches as IReadOnlyList<WorkItemRecord>);
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

    private sealed class StubChatMessageStore(IReadOnlyList<ChatMessage> messages) : IChatMessageStore
    {
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
            return Task.FromResult(Array.Empty<ChatMessage>() as IReadOnlyList<ChatMessage>);
        }

        public Task<IReadOnlyList<ChatMessage>> GetPendingMessagesForConversationAsync(
            Guid userId,
            string source,
            string externalChatId,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(Array.Empty<ChatMessage>() as IReadOnlyList<ChatMessage>);
        }

        public Task<IReadOnlyList<ChatMessage>> GetRecentMessagesAsync(Guid userId, int take, CancellationToken cancellationToken)
        {
            return Task.FromResult(messages.Where(item => item.UserId == userId).Take(take).ToList() as IReadOnlyList<ChatMessage>);
        }

        public Task<IReadOnlyList<ChatMessage>> SearchRecentMessagesAsync(Guid userId, string query, int limit, CancellationToken cancellationToken)
        {
            if (limit <= 0 || string.IsNullOrWhiteSpace(query))
            {
                return Task.FromResult(Array.Empty<ChatMessage>() as IReadOnlyList<ChatMessage>);
            }

            var trimmed = query.Trim();
            var matches = messages
                .Where(message => message.UserId == userId &&
                    (message.Text.Contains(trimmed, StringComparison.OrdinalIgnoreCase) ||
                     message.SenderName.Contains(trimmed, StringComparison.OrdinalIgnoreCase) ||
                     message.ExternalChatId.Contains(trimmed, StringComparison.OrdinalIgnoreCase)))
                .OrderByDescending(message => message.SentAt)
                .Take(limit)
                .ToList();

            return Task.FromResult(matches as IReadOnlyList<ChatMessage>);
        }

        public Task MarkProcessedAsync(IEnumerable<Guid> messageIds, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class StubChatTitleService(IReadOnlyDictionary<string, string> chatTitles) : IChatTitleService
    {
        public Task<IReadOnlyDictionary<string, string>> ResolveManyAsync(
            Guid userId,
            IEnumerable<string> externalChatIds,
            CancellationToken cancellationToken)
        {
            var resolved = externalChatIds
                .Distinct(StringComparer.Ordinal)
                .Where(chatTitles.ContainsKey)
                .ToDictionary(chatId => chatId, chatId => chatTitles[chatId], StringComparer.Ordinal);

            return Task.FromResult(resolved as IReadOnlyDictionary<string, string>);
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow()
        {
            return utcNow;
        }
    }
}
