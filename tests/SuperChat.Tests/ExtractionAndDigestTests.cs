using SuperChat.Contracts.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using SuperChat.Contracts;
using SuperChat.Domain.Model;
using SuperChat.Domain.Services;
using SuperChat.Infrastructure.Abstractions;
using SuperChat.Infrastructure.Persistence;
using SuperChat.Infrastructure.Services;

namespace SuperChat.Tests;

public sealed class ExtractionAndDigestTests
{
    [Fact]
    public async Task HeuristicExtraction_RecognizesTaskAndWaiting()
    {
        var message = new NormalizedMessage(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "telegram",
            "!sales:matrix.localhost",
            "$event-1",
            "Alex",
            "Please send the proposal tomorrow. Still waiting for reply from Marina.",
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow,
            false);

        var service = CreateHeuristicService();
        var items = await service.ExtractAsync(CreateWindow(message), CancellationToken.None);

        Assert.Contains(items, item => item.Kind == ExtractedItemKind.Task);
        Assert.Contains(items, item => item.Kind == ExtractedItemKind.WaitingOn);
    }

    [Fact]
    public async Task HeuristicExtraction_DoesNotCreateGenericFollowUpCandidate()
    {
        var message = new NormalizedMessage(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "telegram",
            "!room:matrix.localhost",
            "$event-plain",
            "Alex",
            "video.mp4",
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow,
            false);

        var service = CreateHeuristicService();
        var items = await service.ExtractAsync(CreateWindow(message), CancellationToken.None);

        Assert.Empty(items);
    }

    [Fact]
    public async Task HeuristicExtraction_IgnoresLongStructuredProductSpec()
    {
        var text = """
            Design a high-fidelity, modern B2B SaaS web app called "SuperChat".
            PRODUCT GOAL
            SuperChat is a business productivity tool that sits on top of Telegram.
            TARGET USERS
            - founders
            - operators
            - sales / bizdev
            MAIN INFORMATION ARCHITECTURE
            1. Login / Invite-only Authentication
            2. Onboarding / Connect Telegram
            3. Main Dashboard: Today
            4. Search / Ask
            8. Примерный дизайн экранов
            ┌────────────────────────────────────────────┐
            │ SuperChat                                 │
            │ [ Email ]                                │
            └────────────────────────────────────────────┘
            """;

        var message = new NormalizedMessage(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "telegram",
            "!room:matrix.localhost",
            "$event-spec",
            "Alex",
            text,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow,
            false);

        var service = CreateHeuristicService();
        var items = await service.ExtractAsync(CreateWindow(message), CancellationToken.None);

        Assert.Empty(items);
    }

    [Fact]
    public async Task HeuristicExtraction_RecognizesMeetingWithExplicitTime()
    {
        var sentAt = new DateTimeOffset(2026, 03, 13, 10, 04, 38, TimeSpan.FromHours(6));
        var message = new NormalizedMessage(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "telegram",
            "!friends:matrix.localhost",
            "$event-2",
            "Stanislav",
            "Мб заехать за тобой в 11?",
            sentAt,
            sentAt,
            false);

        var service = CreateHeuristicService();
        var items = await service.ExtractAsync(CreateWindow(message), CancellationToken.None);
        var meeting = Assert.Single(items, item => item.Kind == ExtractedItemKind.Meeting);

        Assert.Equal(new DateTimeOffset(2026, 03, 13, 08, 00, 00, TimeSpan.Zero), meeting.DueAt);
    }

    [Fact]
    public async Task DeepSeekExtraction_UsesAiStructuredItemsWithVaryingConfidence()
    {
        var sentAt = new DateTimeOffset(2026, 03, 16, 08, 00, 00, TimeSpan.Zero);
        var message = new NormalizedMessage(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "telegram",
            "!sales:matrix.localhost",
            "$event-ai-1",
            "Marina",
            "Напомни, пожалуйста, по договору. Жду финальный ответ сегодня до 18:00 мск.",
            sentAt,
            sentAt,
            false);

        var service = CreateDeepSeekService(new FakeDeepSeekJsonClient(
            new DeepSeekStructuredResponse(
            [
                new DeepSeekStructuredItem(
                    "waiting_on",
                    "Нужно ответить Марине",
                    "Marina",
                    "2026-03-16T15:00:00Z",
                    null,
                    0.94,
                    "Марина ждёт финальный ответ по договору сегодня."),
                new DeepSeekStructuredItem(
                    "task",
                    "Финализировать ответ по договору",
                    "Marina",
                    "2026-03-16T15:00:00Z",
                    null,
                    0.73,
                    "Нужно подготовить и отправить финальный ответ по договору.")
            ])));

        var items = await service.ExtractAsync(CreateWindow(message), CancellationToken.None);

        var waiting = Assert.Single(items, item => item.Kind == ExtractedItemKind.WaitingOn);
        var task = Assert.Single(items, item => item.Kind == ExtractedItemKind.Task);

        Assert.Equal(0.94, waiting.Confidence);
        Assert.Equal(0.73, task.Confidence);
        Assert.Equal(new DateTimeOffset(2026, 03, 16, 15, 00, 00, TimeSpan.Zero), waiting.DueAt);
        Assert.Equal("Нужно ответить Марине", waiting.Title);
    }

    [Fact]
    public async Task DeepSeekExtraction_FallsBackToHeuristics_WhenAiFails()
    {
        var message = new NormalizedMessage(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "telegram",
            "!sales:matrix.localhost",
            "$event-ai-2",
            "Alex",
            "Please send the proposal tomorrow. Still waiting for reply from Marina.",
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow,
            false);

        var service = CreateDeepSeekService(new FakeDeepSeekJsonClient(new InvalidOperationException("boom")));
        var items = await service.ExtractAsync(CreateWindow(message), CancellationToken.None);

        Assert.Contains(items, item => item.Kind == ExtractedItemKind.Task);
        Assert.Contains(items, item => item.Kind == ExtractedItemKind.WaitingOn);
        Assert.All(items, item => Assert.Equal(0.82, item.Confidence));
    }

    [Fact]
    public async Task DeepSeekExtraction_SendsWholeDialogueWindowToModel()
    {
        var userId = Guid.NewGuid();
        var first = new NormalizedMessage(
            Guid.NewGuid(),
            userId,
            "telegram",
            "!sales:matrix.localhost",
            "$event-ai-3",
            "Marina",
            "Напомни, пожалуйста, по договору.",
            new DateTimeOffset(2026, 03, 16, 08, 00, 00, TimeSpan.Zero),
            new DateTimeOffset(2026, 03, 16, 08, 00, 00, TimeSpan.Zero),
            false);
        var second = new NormalizedMessage(
            Guid.NewGuid(),
            userId,
            "telegram",
            "!sales:matrix.localhost",
            "$event-ai-4",
            "You",
            "Да, посмотрю сегодня и вернусь с финальным ответом.",
            new DateTimeOffset(2026, 03, 16, 08, 01, 00, TimeSpan.Zero),
            new DateTimeOffset(2026, 03, 16, 08, 01, 00, TimeSpan.Zero),
            false);

        var fakeClient = new FakeDeepSeekJsonClient(new DeepSeekStructuredResponse([]));
        var service = CreateDeepSeekService(fakeClient);

        await service.ExtractAsync(CreateWindow(first, second), CancellationToken.None);

        var prompt = Assert.Single(fakeClient.LastMessages!, message => message.Role == "user").Content;
        Assert.Contains("Marina: Напомни, пожалуйста, по договору.", prompt, StringComparison.Ordinal);
        Assert.Contains("You: Да, посмотрю сегодня и вернусь с финальным ответом.", prompt, StringComparison.Ordinal);
    }

    [Fact]
    public async Task HeuristicExtraction_RecognizesConfirmedMeetingForTodayInMoscowTime()
    {
        var sentAt = new DateTimeOffset(2026, 03, 13, 09, 15, 00, TimeSpan.Zero);
        var message = new NormalizedMessage(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "telegram",
            "!friends:matrix.localhost",
            "$event-3",
            "Alex",
            "итого, у нас будет встреча в 20:00 по мск времени сегодня, подтверждаю это",
            sentAt,
            sentAt,
            false);

        var service = CreateHeuristicService();
        var items = await service.ExtractAsync(message, CancellationToken.None);
        var meeting = Assert.Single(items, item => item.Kind == ExtractedItemKind.Meeting);

        Assert.Equal(new DateTimeOffset(2026, 03, 13, 17, 00, 00, TimeSpan.Zero), meeting.DueAt);
    }

    [Fact]
    public void MeetingSignalDetector_RecognizesMeetingFromChunkContext()
    {
        var observedAt = new DateTimeOffset(2026, 03, 13, 09, 15, 00, TimeSpan.Zero);
        var chunkText = """
            Alex: давай зафиксируем
            You: итого, у нас будет встреча в 20:00 по мск времени сегодня, подтверждаю это
            Alex: ок
            """;

        var signal = MeetingSignalDetector.TryFromChunk(
            chunkText,
            observedAt,
            observedAt,
            TimeZoneInfo.FindSystemTimeZoneById("Europe/Moscow"));

        Assert.NotNull(signal);
        Assert.Equal("итого, у нас будет встреча в 20:00 по мск времени сегодня, подтверждаю это", signal!.Summary);
        Assert.Equal(new DateTimeOffset(2026, 03, 13, 17, 00, 00, TimeSpan.Zero), signal.ScheduledFor);
    }

    [Fact]
    public void MeetingService_ToMeetingCandidate_IgnoresStructuredArtifactChunk()
    {
        var chunk = new MessageChunkEntity
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            ChatId = "!room:matrix.localhost",
            ContentHash = "spec-hash",
            Text = """
                8. Примерный дизайн экранов
                Экран 3 — Today (главный)
                ┌────────────────────────────────────────────────────────────┐
                │ Созвон с Сергеем — сегодня 15:00                          │
                │ [ Открыть ] [ В календарь позже ]                         │
                └────────────────────────────────────────────────────────────┘
                """,
            TsFrom = new DateTimeOffset(2026, 03, 14, 09, 00, 00, TimeSpan.Zero),
            TsTo = new DateTimeOffset(2026, 03, 14, 09, 00, 00, TimeSpan.Zero)
        };

        var candidate = MeetingService.ToMeetingCandidate(
            chunk,
            TimeZoneInfo.FindSystemTimeZoneById("Europe/Moscow"));

        Assert.Null(candidate);
    }

    [Fact]
    public void DigestComposer_PrioritizesWaitingAndTodayItems()
    {
        var now = DateTimeOffset.UtcNow;
        var items = new[]
        {
            new ExtractedItem(Guid.NewGuid(), Guid.NewGuid(), ExtractedItemKind.Task, "Send contract", "send contract", "!ops", "$1", null, now, now.AddHours(6), 0.9),
            new ExtractedItem(Guid.NewGuid(), Guid.NewGuid(), ExtractedItemKind.WaitingOn, "Waiting on Marina", "waiting", "!sales", "$2", "Marina", now, null, 0.88),
            new ExtractedItem(Guid.NewGuid(), Guid.NewGuid(), ExtractedItemKind.Meeting, "Friday sync", "meeting", "!team", "$3", null, now, now.AddDays(1), 0.77)
        };

        var today = DigestComposer.BuildToday(items, now);
        var waiting = DigestComposer.BuildWaiting(items);

        Assert.Equal(2, today.Count);
        Assert.Single(waiting);
        Assert.Equal("Waiting on Marina", waiting[0].Title);
    }

    [Fact]
    public void DigestComposer_BuildToday_ExcludesItemsFromPreviousDay()
    {
        var now = new DateTimeOffset(2026, 03, 12, 10, 00, 00, TimeSpan.FromHours(3));
        var items = new[]
        {
            new ExtractedItem(Guid.NewGuid(), Guid.NewGuid(), ExtractedItemKind.Task, "Today task", "today", "!ops", "$1", null, now.AddHours(-1), now.AddHours(2), 0.9),
            new ExtractedItem(Guid.NewGuid(), Guid.NewGuid(), ExtractedItemKind.Task, "Yesterday task", "yesterday", "!ops", "$2", null, now.AddDays(-1), now.AddHours(1), 0.8)
        };

        var today = DigestComposer.BuildToday(items, now);

        Assert.Single(today);
        Assert.Equal("Today task", today[0].Title);
    }

    [Fact]
    public void DigestComposer_BuildToday_UsesConfiguredDayBoundary()
    {
        var now = new DateTimeOffset(2026, 03, 12, 00, 10, 00, TimeSpan.FromHours(3));
        var items = new[]
        {
            new ExtractedItem(Guid.NewGuid(), Guid.NewGuid(), ExtractedItemKind.Task, "After midnight Moscow", "today in business timezone", "!ops", "$1", null, new DateTimeOffset(2026, 03, 11, 21, 10, 00, TimeSpan.Zero), now.AddHours(1), 0.9),
            new ExtractedItem(Guid.NewGuid(), Guid.NewGuid(), ExtractedItemKind.Task, "Before midnight Moscow", "previous local day", "!ops", "$2", null, new DateTimeOffset(2026, 03, 11, 20, 50, 00, TimeSpan.Zero), now.AddHours(1), 0.7)
        };

        var today = DigestComposer.BuildToday(items, now);

        Assert.Single(today);
        Assert.Equal("After midnight Moscow", today[0].Title);
    }

    [Fact]
    public void DigestComposer_BuildMeetings_ReturnsNearestUpcomingMeetings()
    {
        var now = new DateTimeOffset(2026, 03, 13, 10, 00, 00, TimeSpan.FromHours(6));
        var meetings = new[]
        {
            new MeetingRecord(Guid.NewGuid(), Guid.NewGuid(), "Upcoming meeting", "Созвон в 11", "!team", "$1", null, now.AddMinutes(-5), now.AddHours(1), 0.8),
            new MeetingRecord(Guid.NewGuid(), Guid.NewGuid(), "Upcoming meeting", "Встреча завтра в 9", "!team", "$2", null, now, now.AddDays(1).AddHours(-1), 0.7),
            new MeetingRecord(Guid.NewGuid(), Guid.NewGuid(), "Upcoming meeting", "Очень дальняя встреча", "!team", "$3", null, now, now.AddDays(30), 0.9)
        };

        var upcoming = DigestComposer.BuildMeetings(meetings, now);

        Assert.Equal(2, upcoming.Count);
        Assert.Equal("Созвон в 11", upcoming[0].Summary);
        Assert.Equal("Встреча завтра в 9", upcoming[1].Summary);
    }

    private static HeuristicStructuredExtractionService CreateHeuristicService()
    {
        return new HeuristicStructuredExtractionService(new PilotOptions
        {
            TodayTimeZoneId = "Europe/Moscow"
        });
    }

    private static DeepSeekStructuredExtractionService CreateDeepSeekService(IDeepSeekJsonClient client)
    {
        return new DeepSeekStructuredExtractionService(
            client,
            new PilotOptions
            {
                TodayTimeZoneId = "Europe/Moscow"
            },
            NullLogger<DeepSeekStructuredExtractionService>.Instance);
    }

    private static ConversationWindow CreateWindow(params NormalizedMessage[] messages)
    {
        var orderedMessages = messages
            .OrderBy(message => message.SentAt)
            .ThenBy(message => message.IngestedAt)
            .ThenBy(message => message.Id)
            .ToList();

        var first = orderedMessages[0];
        return new ConversationWindow(
            first.UserId,
            first.Source,
            first.MatrixRoomId,
            orderedMessages);
    }

    private sealed class FakeDeepSeekJsonClient : IDeepSeekJsonClient
    {
        private readonly DeepSeekStructuredResponse? response;
        private readonly Exception? exception;

        public IReadOnlyList<DeepSeekMessage>? LastMessages { get; private set; }

        public FakeDeepSeekJsonClient(DeepSeekStructuredResponse response)
        {
            this.response = response;
        }

        public FakeDeepSeekJsonClient(Exception exception)
        {
            this.exception = exception;
        }

        public bool IsConfigured => true;

        public Task<TResponse?> CompleteJsonAsync<TResponse>(
            IReadOnlyList<DeepSeekMessage> messages,
            int maxTokens,
            CancellationToken cancellationToken) where TResponse : class
        {
            LastMessages = messages.ToList();

            if (exception is not null)
            {
                throw exception;
            }

            return Task.FromResult(response as TResponse);
        }
    }
}
