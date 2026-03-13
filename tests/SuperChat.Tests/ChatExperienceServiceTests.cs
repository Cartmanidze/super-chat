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
    public async Task AskAsync_RejectsQuestionsOverOneHundredCharacters()
    {
        var service = CreateService();

        var exception = await Assert.ThrowsAsync<ArgumentException>(() => service.AskAsync(
            Guid.NewGuid(),
            new ChatPromptRequest(ChatPromptTemplate.Custom, new string('x', 101)),
            CancellationToken.None));

        Assert.Contains("100", exception.Message, StringComparison.Ordinal);
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

    private static ChatExperienceService CreateService(
        IDigestService? digestService = null,
        IRetrievalService? retrievalService = null,
        ISearchService? searchService = null,
        IMessageNormalizationService? messageNormalizationService = null,
        IRoomDisplayNameService? roomDisplayNameService = null,
        TimeProvider? timeProvider = null)
    {
        return new ChatExperienceService(
            digestService ?? new StubDigestService(),
            retrievalService ?? new StubRetrievalService([]),
            searchService ?? new StubSearchService(),
            messageNormalizationService ?? new StubMessageNormalizationService([]),
            roomDisplayNameService ?? new StubRoomDisplayNameService(new Dictionary<string, string>()),
            timeProvider ?? new StaticTimeProvider(DateTimeOffset.UtcNow),
            new PilotOptions { TodayTimeZoneId = "Europe/Moscow" },
            NullLogger<ChatExperienceService>.Instance);
    }

    private sealed class StubDigestService(
        IReadOnlyList<DashboardCardViewModel>? today = null,
        IReadOnlyList<DashboardCardViewModel>? waiting = null) : IDigestService
    {
        public Task<IReadOnlyList<DashboardCardViewModel>> GetTodayAsync(Guid userId, CancellationToken cancellationToken)
        {
            return Task.FromResult(today ?? Array.Empty<DashboardCardViewModel>());
        }

        public Task<IReadOnlyList<DashboardCardViewModel>> GetWaitingAsync(Guid userId, CancellationToken cancellationToken)
        {
            return Task.FromResult(waiting ?? Array.Empty<DashboardCardViewModel>());
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
