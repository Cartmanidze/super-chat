using Microsoft.Extensions.Logging.Abstractions;
using SuperChat.Contracts.Configuration;
using SuperChat.Domain.Model;
using SuperChat.Infrastructure.Abstractions;
using SuperChat.Infrastructure.Services;

namespace SuperChat.Tests;

public sealed class RoomDisplayNamePresentationTests
{
    [Fact]
    public async Task SearchService_ReplacesRoomIdWithResolvedDisplayName()
    {
        var userId = Guid.NewGuid();
        var service = new SearchService(
            new StubExtractedItemService([
                new ExtractedItem(
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
                    0.9)
            ]),
            new StubMessageNormalizationService([]),
            new StubRoomDisplayNameService(new Dictionary<string, string>
            {
                ["!sales:matrix.localhost"] = "Sales Team"
            }));

        var results = await service.SearchAsync(userId, "contract", CancellationToken.None);

        Assert.Single(results);
        Assert.Equal("Sales Team", results[0].SourceRoom);
    }

    [Fact]
    public async Task SearchService_ReplacesRoomIdForRawMessageFallback()
    {
        var userId = Guid.NewGuid();
        var service = new SearchService(
            new StubExtractedItemService([]),
            new StubMessageNormalizationService([
                new NormalizedMessage(
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
            new StubRoomDisplayNameService(new Dictionary<string, string>
            {
                ["!founders:matrix.localhost"] = "Founders"
            }));

        var results = await service.SearchAsync(userId, "pilot-marker", CancellationToken.None);

        Assert.Single(results);
        Assert.Equal("Founders", results[0].SourceRoom);
    }

    [Fact]
    public async Task SearchService_UsesRoomDisplayNameWhenRawMessageSenderIsOpaqueNumericId()
    {
        var userId = Guid.NewGuid();
        var service = new SearchService(
            new StubExtractedItemService([]),
            new StubMessageNormalizationService([
                new NormalizedMessage(
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
            new StubRoomDisplayNameService(new Dictionary<string, string>
            {
                ["!dm:matrix.localhost"] = "Bi (Telegram)"
            }));

        var results = await service.SearchAsync(userId, "video", CancellationToken.None);

        Assert.Single(results);
        Assert.Equal("Bi", results[0].Title);
        Assert.Equal("Bi (Telegram)", results[0].SourceRoom);
    }

    [Fact]
    public async Task DigestService_ReplacesRoomIdWithResolvedDisplayName()
    {
        var userId = Guid.NewGuid();
        var now = new DateTimeOffset(2026, 03, 12, 12, 00, 00, TimeSpan.Zero);
        var service = new DigestService(
            new StubExtractedItemService([
                new ExtractedItem(
                    Guid.NewGuid(),
                    userId,
                    ExtractedItemKind.Task,
                    "Action needed",
                    "Please send the proposal tomorrow.",
                    "!team:matrix.localhost",
                    "$evt-3",
                    null,
                    now,
                    now.AddHours(2),
                    0.95)
            ]),
            new StubMeetingService([]),
            new StubRoomDisplayNameService(new Dictionary<string, string>
            {
                ["!team:matrix.localhost"] = "Sales Ops"
            }),
            new FixedTimeProvider(now),
            new PilotOptions
            {
                TodayTimeZoneId = "Europe/Moscow"
            },
            NullLogger<DigestService>.Instance);

        var cards = await service.GetTodayAsync(userId, CancellationToken.None);

        Assert.Single(cards);
        Assert.Equal("Sales Ops", cards[0].SourceRoom);
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
    }

    private sealed class StubExtractedItemService(IReadOnlyList<ExtractedItem> items) : IExtractedItemService
    {
        public Task AddRangeAsync(IEnumerable<ExtractedItem> items, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<IReadOnlyList<ExtractedItem>> GetForUserAsync(Guid userId, CancellationToken cancellationToken)
        {
            return Task.FromResult(items.Where(item => item.UserId == userId).ToList() as IReadOnlyList<ExtractedItem>);
        }
    }

    private sealed class StubMessageNormalizationService(IReadOnlyList<NormalizedMessage> messages) : IMessageNormalizationService
    {
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
            return Task.FromResult(Array.Empty<NormalizedMessage>() as IReadOnlyList<NormalizedMessage>);
        }

        public Task<IReadOnlyList<NormalizedMessage>> GetRecentMessagesAsync(Guid userId, int take, CancellationToken cancellationToken)
        {
            return Task.FromResult(messages.Where(item => item.UserId == userId).Take(take).ToList() as IReadOnlyList<NormalizedMessage>);
        }

        public Task MarkProcessedAsync(IEnumerable<Guid> messageIds, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class StubRoomDisplayNameService(IReadOnlyDictionary<string, string> roomNames) : IRoomDisplayNameService
    {
        public Task<IReadOnlyDictionary<string, string>> ResolveManyAsync(
            Guid userId,
            IEnumerable<string> sourceRooms,
            CancellationToken cancellationToken)
        {
            var resolved = sourceRooms
                .Distinct(StringComparer.Ordinal)
                .Where(roomNames.ContainsKey)
                .ToDictionary(roomId => roomId, roomId => roomNames[roomId], StringComparer.Ordinal);

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
