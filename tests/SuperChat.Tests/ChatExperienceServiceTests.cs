using Microsoft.Extensions.Logging.Abstractions;
using SuperChat.Contracts.Features.Chat;
using SuperChat.Contracts.Features.Operations;
using SuperChat.Contracts.Features.WorkItems;
using SuperChat.Domain.Features.Chat;
using SuperChat.Infrastructure.Features.Chat;

namespace SuperChat.Tests;

public sealed class ChatExperienceServiceTests
{
    [Fact]
    public async Task AskAsync_ReturnsMeetingCards_ForMeetingsTemplate()
    {
        var scheduledFor = new DateTimeOffset(2026, 03, 13, 11, 00, 00, TimeSpan.FromHours(6));
        var service = CreateService(
            digestService: new StubDigestService(
            [
                new MeetingWorkItemCardViewModel("Upcoming meeting", "Мб заехать за тобой в 11?", scheduledFor.AddHours(-1), scheduledFor, "Stanislav Klyukhin (Telegram)")
            ]));

        var answer = await service.AskAsync(
            Guid.NewGuid(),
            new ChatPromptRequest(ChatPromptTemplate.Meetings, "Какие у меня ближайшие встречи?"),
            CancellationToken.None);

        Assert.Equal(ChatPromptTemplate.Meetings, answer.Mode);
        var item = Assert.Single(answer.Items);
        Assert.Equal("Мб заехать за тобой в 11?", item.Title);
        Assert.Equal(string.Empty, item.Summary);
        Assert.Equal(scheduledFor, item.Timestamp);
        Assert.IsType<MeetingChatResultItemViewModel>(item);
    }

    [Fact]
    public async Task AskAsync_MeetingsTemplateUsesGeneratedAiAnswerWhenAvailable()
    {
        var service = CreateService(
            digestService: new StubDigestService(
            [
                new MeetingWorkItemCardViewModel("Upcoming meeting", "Созвон с Валерией в 14:00", DateTimeOffset.UtcNow, null, "Личные")
            ]),
            answerGenerationService: new StubChatAnswerGenerationService(
                new GeneratedChatAnswer(
                    "Ближайшая встреча сегодня в 14:00 с Валерией.",
                    [
                        new GeneratedChatAnswerItem("ctx_1", "Созвон с Валерией", "Подтверждённый созвон сегодня в 14:00.")
                    ])));

        var answer = await service.AskAsync(
            Guid.NewGuid(),
            new ChatPromptRequest(ChatPromptTemplate.Meetings, "Какие у меня ближайшие встречи?"),
            CancellationToken.None);

        Assert.Equal("Ближайшая встреча сегодня в 14:00 с Валерией.", answer.AssistantText);
        var item = Assert.Single(answer.Items);
        Assert.Equal("Созвон с Валерией", item.Title);
        Assert.Equal("Подтверждённый созвон сегодня в 14:00.", item.Summary);
        Assert.Equal("Личные", item.ChatTitle);
    }

    [Fact]
    public async Task AskAsync_TemplateKeepsBaseItems_WhenAiReturnsTextWithoutEvidence()
    {
        var service = CreateService(
            digestService: new StubDigestService(
            [
                new MeetingWorkItemCardViewModel("Upcoming meeting", "Мб заехать за тобой в 11?", DateTimeOffset.UtcNow, null, "Stanislav Klyukhin (Telegram)")
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
        Assert.Equal("Мб заехать за тобой в 11?", Assert.Single(answer.Items).Title);
    }

    [Fact]
    public async Task AskAsync_TemplateSuppressesBaseItems_WhenAiReportsNoRelevantContext()
    {
        var service = CreateService(
            digestService: new StubDigestService(
            [
                new MeetingWorkItemCardViewModel("Upcoming meeting", "Черновой слот без подтверждения", DateTimeOffset.UtcNow, null, "Team")
            ]),
            answerGenerationService: new StubChatAnswerGenerationService(
                new GeneratedChatAnswer(
                    "Контекст не содержит информации о ваших ближайших встречах.",
                    [])));

        var answer = await service.AskAsync(
            Guid.NewGuid(),
            new ChatPromptRequest(ChatPromptTemplate.Meetings, "Какие у меня ближайшие встречи?"),
            CancellationToken.None);

        Assert.Equal("Контекст не содержит информации о ваших ближайших встречах.", answer.AssistantText);
        Assert.Empty(answer.Items);
    }

    [Fact]
    public async Task AskAsync_ReturnsMeetingJoinLink_ForMeetingsTemplate()
    {
        var scheduledFor = new DateTimeOffset(2026, 03, 13, 11, 00, 00, TimeSpan.FromHours(6));
        var joinUrl = new Uri("https://meet.google.com/abc-defg-hij");
        var service = CreateService(
            digestService: new StubDigestService(
            [
                new MeetingWorkItemCardViewModel(
                    "Upcoming meeting",
                    "Созвон с командой",
                    scheduledFor.AddHours(-1),
                    scheduledFor,
                    "Stanislav Klyukhin (Telegram)",
                    MeetingStatus: MeetingStatus.Confirmed,
                    Confidence: 0.95,
                    Priority: WorkItemPriority.Important,
                    Owner: WorkItemOwner.Both,
                    Origin: WorkItemOrigin.DetectedFromChat,
                    ReviewState: AiReviewState.Confirmed,
                    PlannedAt: scheduledFor,
                    Source: WorkItemSource.Telegram,
                    UpdatedAt: scheduledFor.AddMinutes(-5),
                    MeetingProvider: MeetingJoinProvider.GoogleMeet,
                    MeetingJoinUrl: joinUrl)
            ]));

        var answer = await service.AskAsync(
            Guid.NewGuid(),
            new ChatPromptRequest(ChatPromptTemplate.Meetings, "Какие у меня ближайшие встречи?"),
            CancellationToken.None);

        var item = Assert.Single(answer.Items);
        Assert.Equal(MeetingJoinProvider.GoogleMeet, item.MeetingProvider);
        Assert.Equal(joinUrl, item.MeetingJoinUrl);
        Assert.Equal(WorkItemType.Meeting, item.Type);
        Assert.Equal(WorkItemStatus.Confirmed, item.Status);
    }

    [Fact]
    public async Task AskAsync_RejectsRemovedTemplate()
    {
        var service = CreateService();

        var exception = await Assert.ThrowsAsync<ArgumentException>(() => service.AskAsync(
            Guid.NewGuid(),
            new ChatPromptRequest("today", "Что для меня важно сегодня?"),
            CancellationToken.None));

        Assert.Contains("Unsupported chat template.", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AskAsync_RejectsQuestionsOverOneHundredCharacters()
    {
        var service = CreateService();

        var exception = await Assert.ThrowsAsync<ArgumentException>(() => service.AskAsync(
            Guid.NewGuid(),
            new ChatPromptRequest(ChatPromptTemplate.Meetings, new string('x', 101)),
            CancellationToken.None));

        Assert.Contains("100", exception.Message, StringComparison.Ordinal);
    }

    private static ChatExperienceService CreateService(
        IDigestService? digestService = null,
        IChatAnswerGenerationService? answerGenerationService = null)
    {
        var resolvedDigestService = digestService ?? new StubDigestService([]);

        return new ChatExperienceService(
            new ChatTemplateCatalog(),
            [new MeetingsChatTemplateHandler(resolvedDigestService)],
            answerGenerationService ?? new StubChatAnswerGenerationService(null),
            NullLogger<ChatExperienceService>.Instance);
    }

    private sealed class StubDigestService(IReadOnlyList<WorkItemCardViewModel> meetings) : IDigestService
    {
        public Task<IReadOnlyList<WorkItemCardViewModel>> GetMeetingsAsync(Guid userId, CancellationToken cancellationToken)
        {
            return Task.FromResult(meetings);
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
}
