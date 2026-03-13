using Microsoft.Extensions.Logging.Abstractions;
using SuperChat.Contracts.Configuration;
using SuperChat.Contracts.ViewModels;
using SuperChat.Domain.Model;
using SuperChat.Infrastructure.Abstractions;
using SuperChat.Infrastructure.Services;

namespace SuperChat.Tests;

public sealed class ChatExperienceServiceTests
{
    [Fact]
    public async Task AskAsync_ReturnsTodayCards_ForTodayTemplate()
    {
        var service = CreateService(
            digestService: new StubDigestService(
                today:
                [
                    new DashboardCardViewModel("Нужно отправить договор", "Свериться с Валерией", "Task", null, "Личные")
                ]));

        var answer = await service.AskAsync(
            Guid.NewGuid(),
            new ChatPromptRequest(ChatPromptTemplate.Today, "Что для меня важно сегодня?"),
            CancellationToken.None);

        Assert.Equal(ChatPromptTemplate.Today, answer.Mode);
        Assert.Single(answer.Items);
        Assert.Equal("Нужно отправить договор", answer.Items[0].Title);
    }

    [Fact]
    public async Task AskAsync_TodayTemplateUsesGeneratedAiAnswerWhenAvailable()
    {
        var service = CreateService(
            digestService: new StubDigestService(
                today:
                [
                    new DashboardCardViewModel("Нужно отправить договор", "Свериться с Валерией", "Task", null, "Личные")
                ]),
            answerGenerationService: new StubChatAnswerGenerationService(
                new GeneratedChatAnswer(
                    "Сегодня главное закрыть вопрос с договором и свериться с Валерией.",
                    [
                        new GeneratedChatAnswerItem("ctx_1", "Главный приоритет", "Нужно отправить договор и подтвердить детали с Валерией.")
                    ])));

        var answer = await service.AskAsync(
            Guid.NewGuid(),
            new ChatPromptRequest(ChatPromptTemplate.Today, "Что для меня важно сегодня?"),
            CancellationToken.None);

        Assert.Equal(ChatPromptTemplate.Today, answer.Mode);
        Assert.Equal("Сегодня главное закрыть вопрос с договором и свериться с Валерией.", answer.AssistantText);
        var item = Assert.Single(answer.Items);
        Assert.Equal("Главный приоритет", item.Title);
        Assert.Equal("Нужно отправить договор и подтвердить детали с Валерией.", item.Summary);
        Assert.Equal("Личные", item.SourceRoom);
    }

    [Fact]
    public async Task AskAsync_TemplateKeepsBaseItems_WhenAiReturnsTextWithoutEvidence()
    {
        var service = CreateService(
            digestService: new StubDigestService(
                meetings:
                [
                    new DashboardCardViewModel("Upcoming meeting", "Мб заехать за тобой в 11?", "Meeting", null, "Stanislav Klyukhin (Telegram)")
                ]),
            answerGenerationService: new StubChatAnswerGenerationService(
                new GeneratedChatAnswer(
                    "Похоже, ближайшая встреча сегодня в 11.",
                    [])));

        var answer = await service.AskAsync(
            Guid.NewGuid(),
            new ChatPromptRequest(ChatPromptTemplate.Meetings, "Какие у меня ближайшие встречи?"),
            CancellationToken.None);

        Assert.Equal("Похоже, ближайшая встреча сегодня в 11.", answer.AssistantText);
        var item = Assert.Single(answer.Items);
        Assert.Equal("Мб заехать за тобой в 11?", item.Title);
        Assert.Equal("Stanislav Klyukhin (Telegram)", item.SourceRoom);
    }

    [Fact]
    public async Task AskAsync_RejectsQuestionsOverOneHundredCharacters()
    {
        var service = CreateService();

        var exception = await Assert.ThrowsAsync<ArgumentException>(() => service.AskAsync(
            Guid.NewGuid(),
            new ChatPromptRequest(ChatPromptTemplate.Today, new string('x', 101)),
            CancellationToken.None));

        Assert.Contains("100", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AskAsync_ReturnsMeetingCards_ForMeetingsTemplate()
    {
        var scheduledFor = new DateTimeOffset(2026, 03, 13, 11, 00, 00, TimeSpan.FromHours(6));
        var service = CreateService(
            digestService: new StubDigestService(
                meetings:
                [
                    new DashboardCardViewModel("Upcoming meeting", "Мб заехать за тобой в 11?", "Meeting", scheduledFor, "Stanislav Klyukhin (Telegram)")
                ]));

        var answer = await service.AskAsync(
            Guid.NewGuid(),
            new ChatPromptRequest(ChatPromptTemplate.Meetings, "Какие у меня ближайшие встречи?"),
            CancellationToken.None);

        Assert.Equal(ChatPromptTemplate.Meetings, answer.Mode);
        Assert.Single(answer.Items);
        Assert.Equal("Мб заехать за тобой в 11?", answer.Items[0].Title);
        Assert.Equal(string.Empty, answer.Items[0].Summary);
        Assert.Equal(scheduledFor, answer.Items[0].Timestamp);
    }

    [Fact]
    public async Task AskAsync_WaitingTemplate_HidesGenericAwaitingResponseTitle()
    {
        var service = CreateService(
            digestService: new StubDigestService(
                waiting:
                [
                    new DashboardCardViewModel("Awaiting response", "Нужен ответ от Валерии по договору", "WaitingOn", null, "Валерия (Telegram)")
                ]));

        var answer = await service.AskAsync(
            Guid.NewGuid(),
            new ChatPromptRequest(ChatPromptTemplate.Waiting, "Где я сейчас жду ответа?"),
            CancellationToken.None);

        Assert.Equal(ChatPromptTemplate.Waiting, answer.Mode);
        Assert.Single(answer.Items);
        Assert.Equal("Нужен ответ от Валерии по договору", answer.Items[0].Title);
        Assert.Equal(string.Empty, answer.Items[0].Summary);
    }

    [Fact]
    public async Task AskAsync_CustomQuestionFallsBackToTokenSearch()
    {
        var searchService = new StubSearchService();
        searchService.Add("договору",
        [
            new SearchResultViewModel("Валерия", "Ждёт договор", "Message", "Личные", DateTimeOffset.UtcNow)
        ]);

        var service = CreateService(searchService: searchService);

        var answer = await service.AskAsync(
            Guid.NewGuid(),
            new ChatPromptRequest(ChatPromptTemplate.Custom, "Что с договору у Валерии?"),
            CancellationToken.None);

        Assert.Equal(ChatPromptTemplate.Custom, answer.Mode);
        Assert.Single(answer.Items);
        Assert.Equal("Валерия", answer.Items[0].Title);
    }

    [Fact]
    public async Task AskAsync_CustomQuestionUsesRetrievalBeforeTokenSearch()
    {
        var retrievalService = new StubRetrievalService(
        [
            new RetrievedChunk(
                Guid.NewGuid(),
                "!room-1",
                "ivan",
                "dialog_chunk",
                "Ivan: Please send the proposal tomorrow.\nYou: I will send it today.",
                DateTimeOffset.UtcNow.AddMinutes(-15),
                DateTimeOffset.UtcNow.AddMinutes(-5),
                0.92)
        ]);

        var service = CreateService(
            retrievalService: retrievalService,
            roomDisplayNameService: new StubRoomDisplayNameService(new Dictionary<string, string> { ["!room-1"] = "Иван" }),
            searchService: new StubSearchService());

        var answer = await service.AskAsync(
            Guid.NewGuid(),
            new ChatPromptRequest(ChatPromptTemplate.Custom, "Что я обещал Ивану?"),
            CancellationToken.None);

        Assert.Equal(ChatPromptTemplate.Custom, answer.Mode);
        Assert.Single(answer.Items);
        Assert.Equal("Ivan: Please send the proposal tomorrow.", answer.Items[0].Title);
        Assert.Equal("Иван", answer.Items[0].SourceRoom);
    }

    [Fact]
    public async Task AskAsync_CustomQuestionUsesGeneratedAiAnswerWhenAvailable()
    {
        var retrievalService = new StubRetrievalService(
        [
            new RetrievedChunk(
                Guid.NewGuid(),
                "!room-1",
                "ivan",
                "dialog_chunk",
                "Ivan: Please send the proposal tomorrow.\nYou: I will send it today.",
                DateTimeOffset.UtcNow.AddMinutes(-15),
                DateTimeOffset.UtcNow.AddMinutes(-5),
                0.92)
        ]);

        var answerGenerationService = new StubChatAnswerGenerationService(
            new GeneratedChatAnswer(
                "You promised Ivan that you would send the proposal today.",
                [
                    new GeneratedChatAnswerItem("ctx_1", "Promise to Ivan", "You explicitly said you would send the proposal today.")
                ]));

        var service = CreateService(
            retrievalService: retrievalService,
            answerGenerationService: answerGenerationService,
            roomDisplayNameService: new StubRoomDisplayNameService(new Dictionary<string, string> { ["!room-1"] = "Иван" }),
            searchService: new StubSearchService());

        var answer = await service.AskAsync(
            Guid.NewGuid(),
            new ChatPromptRequest(ChatPromptTemplate.Custom, "Что я обещал Ивану?"),
            CancellationToken.None);

        Assert.Equal(ChatPromptTemplate.Custom, answer.Mode);
        Assert.Equal("You promised Ivan that you would send the proposal today.", answer.AssistantText);
        Assert.Single(answer.Items);
        Assert.Equal("Promise to Ivan", answer.Items[0].Title);
        Assert.Equal("Иван", answer.Items[0].SourceRoom);
    }

    [Fact]
    public async Task AskAsync_FiltersRecentMessagesToConfiguredDay()
    {
        var timeProvider = new StaticTimeProvider(new DateTimeOffset(2026, 03, 12, 10, 00, 00, TimeSpan.Zero));
        var messages = new[]
        {
            new NormalizedMessage(Guid.NewGuid(), Guid.NewGuid(), "telegram", "!room-1", "$1", "Alice", "Сегодняшнее сообщение", new DateTimeOffset(2026, 03, 12, 08, 00, 00, TimeSpan.Zero), DateTimeOffset.UtcNow, true),
            new NormalizedMessage(Guid.NewGuid(), Guid.NewGuid(), "telegram", "!room-1", "$2", "Alice", "Старое сообщение", new DateTimeOffset(2026, 03, 11, 08, 00, 00, TimeSpan.Zero), DateTimeOffset.UtcNow, true)
        };

        var service = CreateService(
            messageNormalizationService: new StubMessageNormalizationService(messages),
            roomDisplayNameService: new StubRoomDisplayNameService(new Dictionary<string, string> { ["!room-1"] = "Личные" }),
            timeProvider: timeProvider);

        var answer = await service.AskAsync(
            Guid.NewGuid(),
            new ChatPromptRequest(ChatPromptTemplate.Recent, "Что было в сообщениях сегодня?"),
            CancellationToken.None);

        Assert.Equal(ChatPromptTemplate.Recent, answer.Mode);
        Assert.Single(answer.Items);
        Assert.Equal("Сегодняшнее сообщение", answer.Items[0].Summary);
    }

    [Fact]
    public async Task AskAsync_RecentUsesRoomDisplayNameWhenSenderNameIsOpaqueNumericId()
    {
        var userId = Guid.NewGuid();
        var now = new DateTimeOffset(2026, 03, 13, 10, 00, 00, TimeSpan.Zero);
        var messages = new[]
        {
            new NormalizedMessage(
                Guid.NewGuid(),
                userId,
                "telegram",
                "!room-1",
                "$1",
                "349223531",
                "video.mp4",
                now.AddMinutes(-5),
                now.AddMinutes(-4),
                true)
        };

        var service = CreateService(
            messageNormalizationService: new StubMessageNormalizationService(messages),
            roomDisplayNameService: new StubRoomDisplayNameService(new Dictionary<string, string> { ["!room-1"] = "Bi (Telegram)" }),
            timeProvider: new StaticTimeProvider(now));

        var answer = await service.AskAsync(
            userId,
            new ChatPromptRequest(ChatPromptTemplate.Recent, "Что было в сообщениях сегодня?"),
            CancellationToken.None);

        Assert.Single(answer.Items);
        Assert.Equal("Bi", answer.Items[0].Title);
        Assert.Equal("Bi (Telegram)", answer.Items[0].SourceRoom);
    }

    private static ChatExperienceService CreateService(
        IDigestService? digestService = null,
        IChatAnswerGenerationService? answerGenerationService = null,
        IRetrievalService? retrievalService = null,
        ISearchService? searchService = null,
        IMessageNormalizationService? messageNormalizationService = null,
        IRoomDisplayNameService? roomDisplayNameService = null,
        TimeProvider? timeProvider = null)
    {
        var resolvedDigestService = digestService ?? new StubDigestService();
        var resolvedMessageNormalizationService = messageNormalizationService ?? new StubMessageNormalizationService([]);
        var resolvedRoomDisplayNameService = roomDisplayNameService ?? new StubRoomDisplayNameService(new Dictionary<string, string>());
        var resolvedTimeProvider = timeProvider ?? new StaticTimeProvider(DateTimeOffset.UtcNow);
        var pilotOptions = new PilotOptions { TodayTimeZoneId = "Europe/Moscow" };

        var handlers = new IChatTemplateHandler[]
        {
            new TodayChatTemplateHandler(resolvedDigestService),
            new WaitingChatTemplateHandler(resolvedDigestService),
            new MeetingsChatTemplateHandler(resolvedDigestService),
            new RecentChatTemplateHandler(
                resolvedMessageNormalizationService,
                resolvedRoomDisplayNameService,
                resolvedTimeProvider,
                pilotOptions,
                NullLogger<RecentChatTemplateHandler>.Instance)
        };

        return new ChatExperienceService(
            new ChatTemplateCatalog(),
            handlers,
            answerGenerationService ?? new StubChatAnswerGenerationService(null),
            retrievalService ?? new StubRetrievalService([]),
            searchService ?? new StubSearchService(),
            resolvedRoomDisplayNameService,
            NullLogger<ChatExperienceService>.Instance);
    }

    private sealed class StubDigestService(
        IReadOnlyList<DashboardCardViewModel>? today = null,
        IReadOnlyList<DashboardCardViewModel>? waiting = null,
        IReadOnlyList<DashboardCardViewModel>? meetings = null) : IDigestService
    {
        public Task<IReadOnlyList<DashboardCardViewModel>> GetTodayAsync(Guid userId, CancellationToken cancellationToken)
        {
            return Task.FromResult(today ?? Array.Empty<DashboardCardViewModel>());
        }

        public Task<IReadOnlyList<DashboardCardViewModel>> GetWaitingAsync(Guid userId, CancellationToken cancellationToken)
        {
            return Task.FromResult(waiting ?? Array.Empty<DashboardCardViewModel>());
        }

        public Task<IReadOnlyList<DashboardCardViewModel>> GetMeetingsAsync(Guid userId, CancellationToken cancellationToken)
        {
            return Task.FromResult(meetings ?? Array.Empty<DashboardCardViewModel>());
        }
    }

    private sealed class StubSearchService : ISearchService
    {
        private readonly Dictionary<string, IReadOnlyList<SearchResultViewModel>> _results = new(StringComparer.OrdinalIgnoreCase);

        public void Add(string query, IReadOnlyList<SearchResultViewModel> results)
        {
            _results[query] = results;
        }

        public Task<IReadOnlyList<SearchResultViewModel>> SearchAsync(Guid userId, string query, CancellationToken cancellationToken)
        {
            return Task.FromResult(_results.TryGetValue(query, out var results)
                ? results
                : Array.Empty<SearchResultViewModel>());
        }
    }

    private sealed class StubRetrievalService(IReadOnlyList<RetrievedChunk> results) : IRetrievalService
    {
        public Task<IReadOnlyList<RetrievedChunk>> RetrieveAsync(RetrievalRequest request, CancellationToken cancellationToken)
        {
            return Task.FromResult(results);
        }
    }

    private sealed class StubChatAnswerGenerationService(GeneratedChatAnswer? result) : IChatAnswerGenerationService
    {
        public Task<GeneratedChatAnswer?> TryGenerateAsync(
            string question,
            IReadOnlyList<ChatAnswerContextItem> contextItems,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(result);
        }
    }

    private sealed class StubMessageNormalizationService(IReadOnlyList<NormalizedMessage> messages) : IMessageNormalizationService
    {
        public Task<bool> TryStoreAsync(Guid userId, string roomId, string eventId, string senderName, string text, DateTimeOffset sentAt, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<IReadOnlyList<NormalizedMessage>> GetPendingMessagesAsync(CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<IReadOnlyList<NormalizedMessage>> GetRecentMessagesAsync(Guid userId, int take, CancellationToken cancellationToken)
        {
            return Task.FromResult(messages.Take(take).ToList() as IReadOnlyList<NormalizedMessage>);
        }

        public Task MarkProcessedAsync(IEnumerable<Guid> messageIds, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class StubRoomDisplayNameService(IReadOnlyDictionary<string, string> names) : IRoomDisplayNameService
    {
        public Task<IReadOnlyDictionary<string, string>> ResolveManyAsync(Guid userId, IEnumerable<string> sourceRooms, CancellationToken cancellationToken)
        {
            var resolved = sourceRooms
                .Distinct(StringComparer.Ordinal)
                .Where(names.ContainsKey)
                .ToDictionary(sourceRoom => sourceRoom, sourceRoom => names[sourceRoom], StringComparer.Ordinal);

            return Task.FromResult(resolved as IReadOnlyDictionary<string, string>);
        }
    }

    private sealed class StaticTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
