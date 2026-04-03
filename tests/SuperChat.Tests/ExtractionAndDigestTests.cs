using Microsoft.Extensions.Logging.Abstractions;
using SuperChat.Contracts;
using SuperChat.Contracts.Features.Intelligence.Extraction;
using SuperChat.Contracts.Features.Intelligence.Retrieval;
using Microsoft.Extensions.Logging;
using SuperChat.Contracts.Features.Auth;
using SuperChat.Domain.Features.Intelligence;
using SuperChat.Domain.Features.Messaging;
using SuperChat.Infrastructure.Features.Intelligence.Extraction;
using SuperChat.Infrastructure.Features.Intelligence.Meetings;
using SuperChat.Infrastructure.Shared.Persistence;

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

        Assert.Contains(items, item => item.Kind == ExtractedItemKind.Task && item.Title == "Нужен следующий шаг");
        Assert.Contains(items, item => item.Kind == ExtractedItemKind.WaitingOn && item.Title == "Нужно ответить: Alex");
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
            3. Main Today View
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
    public async Task HeuristicExtraction_RecognizesMeetingSlangMitWithExplicitTime()
    {
        var sentAt = new DateTimeOffset(2026, 03, 21, 12, 00, 00, TimeSpan.Zero);
        var message = new NormalizedMessage(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "telegram",
            "!friends:matrix.localhost",
            "$event-mit-1",
            "Stanislav",
            "\u0441\u0435\u0433\u043e\u0434\u043d\u044f \u043c\u0438\u0442 \u0432 18:00",
            sentAt,
            sentAt,
            false);

        var service = CreateHeuristicService();
        var items = await service.ExtractAsync(CreateWindow(message), CancellationToken.None);
        var meeting = Assert.Single(items, item => item.Kind == ExtractedItemKind.Meeting);

        Assert.Equal(new DateTimeOffset(2026, 03, 21, 15, 00, 00, TimeSpan.Zero), meeting.DueAt);
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

        var task = Assert.Single(items, item => item.Kind == ExtractedItemKind.Task);
        var waiting = Assert.Single(items, item => item.Kind == ExtractedItemKind.WaitingOn);

        Assert.Equal("Нужен следующий шаг", task.Title);
        Assert.Equal("Нужно ответить: Alex", waiting.Title);
        Assert.NotEqual(task.Confidence, waiting.Confidence);
        Assert.DoesNotContain(items, item => item.Confidence == 0.82);
    }

    [Fact]
    public async Task DeepSeekExtraction_FallsBackToHeuristics_WhenAiReturnsEmptyItems()
    {
        var message = new NormalizedMessage(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "telegram",
            "!sales:matrix.localhost",
            "$event-ai-empty-1",
            "Alex",
            "Please send the proposal tomorrow. Still waiting for reply from Marina.",
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow,
            false);

        var logger = new RecordingLogger<DeepSeekStructuredExtractionService>();
        var service = CreateDeepSeekService(
            new FakeDeepSeekJsonClient(new DeepSeekStructuredResponse([])),
            logger);

        var items = await service.ExtractAsync(CreateWindow(message), CancellationToken.None);

        var task = Assert.Single(items, item => item.Kind == ExtractedItemKind.Task);
        var waiting = Assert.Single(items, item => item.Kind == ExtractedItemKind.WaitingOn);
        Assert.Equal("\u041d\u0443\u0436\u0435\u043d \u0441\u043b\u0435\u0434\u0443\u044e\u0449\u0438\u0439 \u0448\u0430\u0433", task.Title);
        Assert.Equal("\u041d\u0443\u0436\u043d\u043e \u043e\u0442\u0432\u0435\u0442\u0438\u0442\u044c: Alex", waiting.Title);
        Assert.Contains(
            logger.Messages,
            entry => entry.Contains("Structured extraction completed", StringComparison.Ordinal) &&
                     entry.Contains("UsedFallback=True", StringComparison.Ordinal));
    }

    [Fact]
    public async Task DeepSeekExtraction_DeterministicMeetingOverridesStaleAiSchedule()
    {
        var userId = Guid.NewGuid();
        var first = new NormalizedMessage(
            Guid.NewGuid(),
            userId,
            "telegram",
            "!friends:matrix.localhost",
            "$event-ai-meeting-1",
            "glebov84",
            "назначаю встречу сегодня в 20:00 по мск",
            new DateTimeOffset(2026, 03, 13, 09, 00, 00, TimeSpan.Zero),
            new DateTimeOffset(2026, 03, 13, 09, 00, 00, TimeSpan.Zero),
            false);
        var second = new NormalizedMessage(
            Guid.NewGuid(),
            userId,
            "telegram",
            "!friends:matrix.localhost",
            "$event-ai-meeting-2",
            "Stas (Telegram)",
            "в 20 не могу давай завтра в 19",
            new DateTimeOffset(2026, 03, 13, 09, 01, 00, TimeSpan.Zero),
            new DateTimeOffset(2026, 03, 13, 09, 01, 00, TimeSpan.Zero),
            false);
        var third = new NormalizedMessage(
            Guid.NewGuid(),
            userId,
            "telegram",
            "!friends:matrix.localhost",
            "$event-ai-meeting-3",
            "glebov84",
            "хорошо",
            new DateTimeOffset(2026, 03, 13, 09, 02, 00, TimeSpan.Zero),
            new DateTimeOffset(2026, 03, 13, 09, 02, 00, TimeSpan.Zero),
            false);

        var service = CreateDeepSeekService(new FakeDeepSeekJsonClient(
            new DeepSeekStructuredResponse(
            [
                new DeepSeekStructuredItem(
                    "meeting",
                    "Скоро встреча",
                    null,
                    "2026-03-13T17:00:00Z",
                    null,
                    0.78,
                    "назначаю встречу сегодня в 20:00 по мск")
            ])));

        var items = await service.ExtractAsync(CreateWindow(first, second, third), CancellationToken.None);
        var meeting = Assert.Single(items, item => item.Kind == ExtractedItemKind.Meeting);

        Assert.Equal("в 20 не могу давай завтра в 19", meeting.Summary);
        Assert.Equal(new DateTimeOffset(2026, 03, 14, 16, 00, 00, TimeSpan.Zero), meeting.DueAt);
        Assert.Equal("$event-ai-meeting-3", meeting.SourceEventId);
    }

    [Fact]
    public async Task HeuristicExtraction_DropsWaitingWhenUserAlreadyAnsweredInWindow()
    {
        var userId = Guid.NewGuid();
        var first = new NormalizedMessage(
            Guid.NewGuid(),
            userId,
            "telegram",
            "!sales:matrix.localhost",
            "$event-waiting-1",
            "Marina",
            "Жду ответ по договору.",
            new DateTimeOffset(2026, 03, 16, 08, 00, 00, TimeSpan.Zero),
            new DateTimeOffset(2026, 03, 16, 08, 00, 00, TimeSpan.Zero),
            false);
        var second = new NormalizedMessage(
            Guid.NewGuid(),
            userId,
            "telegram",
            "!sales:matrix.localhost",
            "$event-waiting-2",
            "You",
            "Да, отвечу сегодня после обеда.",
            new DateTimeOffset(2026, 03, 16, 08, 01, 00, TimeSpan.Zero),
            new DateTimeOffset(2026, 03, 16, 08, 01, 00, TimeSpan.Zero),
            false);

        var service = CreateHeuristicService();
        var items = await service.ExtractAsync(CreateWindow(first, second), CancellationToken.None);

        Assert.DoesNotContain(items, item => item.Kind == ExtractedItemKind.WaitingOn);
    }

    [Fact]
    public async Task DeepSeekExtraction_DropsWaitingWhenLatestMeaningfulTurnIsFromUser()
    {
        var userId = Guid.NewGuid();
        var first = new NormalizedMessage(
            Guid.NewGuid(),
            userId,
            "telegram",
            "!sales:matrix.localhost",
            "$event-ai-answered-1",
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
            "$event-ai-answered-2",
            "You",
            "Да, посмотрю сегодня и вернусь с ответом.",
            new DateTimeOffset(2026, 03, 16, 08, 01, 00, TimeSpan.Zero),
            new DateTimeOffset(2026, 03, 16, 08, 01, 00, TimeSpan.Zero),
            false);

        var service = CreateDeepSeekService(new FakeDeepSeekJsonClient(
            new DeepSeekStructuredResponse(
            [
                new DeepSeekStructuredItem(
                    "waiting_on",
                    "Нужно ответить",
                    "Марина",
                    null,
                    null,
                    0.91,
                    "Марина ждёт ответ по договору.")
            ])));

        var items = await service.ExtractAsync(CreateWindow(first, second), CancellationToken.None);

        Assert.DoesNotContain(items, item => item.Kind == ExtractedItemKind.WaitingOn);
    }

    [Fact]
    public async Task DeepSeekExtraction_BackfillsWaitingCounterpartyFromLatestExternalTurn()
    {
        var message = new NormalizedMessage(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "telegram",
            "!sales:matrix.localhost",
            "$event-ai-5",
            "Марина",
            "Напомни, пожалуйста, по договору.",
            new DateTimeOffset(2026, 03, 16, 08, 00, 00, TimeSpan.Zero),
            new DateTimeOffset(2026, 03, 16, 08, 00, 00, TimeSpan.Zero),
            false);

        var service = CreateDeepSeekService(new FakeDeepSeekJsonClient(
            new DeepSeekStructuredResponse(
            [
                new DeepSeekStructuredItem(
                    "waiting_on",
                    "Нужно ответить",
                    null,
                    null,
                    null,
                    0.89,
                    "Нужно вернуться с ответом по договору.")
            ])));

        var items = await service.ExtractAsync(CreateWindow(message), CancellationToken.None);
        var waiting = Assert.Single(items, item => item.Kind == ExtractedItemKind.WaitingOn);

        Assert.Equal("Марина", waiting.Person);
        Assert.Equal("Нужно ответить: Марина", waiting.Title);
        Assert.Equal("$event-ai-5", waiting.SourceEventId);
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
    public async Task DeepSeekExtraction_PromptMarksUnansweredOutreachQuestionsAsWaitingOnCandidates()
    {
        var message = new NormalizedMessage(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "telegram",
            "!sales:matrix.localhost",
            "$event-ai-6",
            "Maria",
            "Добрый день, Глеб! Подскажите, актуально ли для Вас сейчас предложение о работе?",
            new DateTimeOffset(2026, 03, 16, 09, 13, 22, TimeSpan.Zero),
            new DateTimeOffset(2026, 03, 16, 09, 13, 22, TimeSpan.Zero),
            false);

        var fakeClient = new FakeDeepSeekJsonClient(new DeepSeekStructuredResponse([]));
        var service = CreateDeepSeekService(fakeClient);

        await service.ExtractAsync(CreateWindow(message), CancellationToken.None);

        var systemPrompt = Assert.Single(fakeClient.LastMessages!, item => item.Role == "system").Content;
        var userPrompt = Assert.Single(fakeClient.LastMessages!, item => item.Role == "user").Content;

        Assert.Contains("Introductory recruiter, sales, vendor, or partnership outreach still counts as \"waiting_on\"", systemPrompt, StringComparison.Ordinal);
        Assert.Contains("An explicit inbound question to the user usually counts as \"waiting_on\"", systemPrompt, StringComparison.Ordinal);
        Assert.Contains("latest_meaningful_sender: Maria", userPrompt, StringComparison.Ordinal);
        Assert.Contains("unanswered_external_turn: true", userPrompt, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DeepSeekExtraction_LogsHowStructuredItemsAreMapped()
    {
        var message = new NormalizedMessage(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "telegram",
            "!sales:matrix.localhost",
            "$event-ai-7",
            "Marina",
            "Напомни, пожалуйста, по договору.",
            new DateTimeOffset(2026, 03, 16, 08, 00, 00, TimeSpan.Zero),
            new DateTimeOffset(2026, 03, 16, 08, 00, 00, TimeSpan.Zero),
            false);

        var logger = new RecordingLogger<DeepSeekStructuredExtractionService>();
        var service = CreateDeepSeekService(
            new FakeDeepSeekJsonClient(
                new DeepSeekStructuredResponse(
                [
                    new DeepSeekStructuredItem(
                        "waiting_on",
                        "Нужно ответить Марине",
                        "Marina",
                        null,
                        null,
                        0.91,
                        "Марина ждёт ответ по договору."),
                    new DeepSeekStructuredItem(
                        "nonsense_kind",
                        "Шум",
                        null,
                        null,
                        null,
                        0.11,
                        "Шум")
                ])),
            logger);

        var items = await service.ExtractAsync(CreateWindow(message), CancellationToken.None);

        Assert.Single(items);
        Assert.Contains(
            logger.Messages,
            entry => entry.Contains("Structured extraction AI response received", StringComparison.Ordinal) &&
                     entry.Contains("RawItemCount=2", StringComparison.Ordinal));
        Assert.Contains(
            logger.Messages,
            entry => entry.Contains("Structured extraction item mapping completed", StringComparison.Ordinal) &&
                     entry.Contains("RawItemCount=2", StringComparison.Ordinal) &&
                     entry.Contains("MappedItemCount=1", StringComparison.Ordinal) &&
                     entry.Contains("UnknownKindCount=1", StringComparison.Ordinal) &&
                     entry.Contains("MappedKinds=WaitingOn", StringComparison.Ordinal));
    }

    [Fact]
    public async Task HeuristicExtraction_RecognizesOwnCommitmentWithRussianTitle()
    {
        var message = new NormalizedMessage(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "telegram",
            "!sales:matrix.localhost",
            "$event-commitment-1",
            "You",
            "Да, отвечу сегодня после обеда и пришлю финальную версию.",
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow,
            false);

        var service = CreateHeuristicService();
        var items = await service.ExtractAsync(CreateWindow(message), CancellationToken.None);
        var commitment = Assert.Single(items, item => item.Kind == ExtractedItemKind.Commitment);

        Assert.Equal("Ты пообещал", commitment.Title);
        Assert.True(commitment.Confidence > 0.60);
    }

    [Fact]
    public async Task HeuristicExtraction_UsesTextEnrichmentForCounterpartyAndDueAt()
    {
        var sentAt = new DateTimeOffset(2026, 03, 16, 09, 13, 22, TimeSpan.Zero);
        var message = new NormalizedMessage(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "telegram",
            "!sales:matrix.localhost",
            "$event-enriched-1",
            "6143419153",
            "Добрый день, Глеб! Меня зовут Мария, я представляю компанию Umbrella IT. Подскажите, актуально ли для Вас предложение о работе сегодня до 18:00?",
            sentAt,
            sentAt,
            false);

        var service = CreateHeuristicService(new FakeTextEnrichmentClient(
            new TextEnrichmentResponse(
                "Мария",
                "Umbrella IT",
                [
                    new TextEnrichmentEntity("Глеб", "PERSON", "Глеб"),
                    new TextEnrichmentEntity("Мария", "PERSON", "Мария"),
                    new TextEnrichmentEntity("Umbrella IT", "ORG", "Umbrella IT")
                ],
                [
                    new TextEnrichmentTemporalExpression("сегодня до 18:00", "2026-03-16T15:00:00Z", "datetime")
                ])));

        var items = await service.ExtractAsync(CreateWindow(message), CancellationToken.None);
        var waiting = Assert.Single(items, item => item.Kind == ExtractedItemKind.WaitingOn);

        Assert.Equal("Мария", waiting.Person);
        Assert.Equal("Нужно ответить: Мария", waiting.Title);
        Assert.Equal(new DateTimeOffset(2026, 03, 16, 15, 00, 00, TimeSpan.Zero), waiting.DueAt);
    }

    [Fact]
    public async Task HeuristicExtraction_RecoversMeetingFromTemporalEnrichment_WhenRuleDetectionMisses()
    {
        var sentAt = new DateTimeOffset(2026, 03, 21, 06, 53, 55, TimeSpan.Zero);
        var message = new NormalizedMessage(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "telegram",
            "!friends:matrix.localhost",
            "$event-enriched-meeting-1",
            "You",
            "\u0441\u043e\u0431\u0435\u0441\u0435\u0434\u043e\u0432\u0430\u043d\u0438\u0435 \u0447\u0435\u0440\u0435\u0437 \u0447\u0430\u0441",
            sentAt,
            sentAt,
            false);

        var service = CreateHeuristicService(new FakeTextEnrichmentClient(
            new TextEnrichmentResponse(
                null,
                null,
                [],
                [
                    new TextEnrichmentTemporalExpression("\u0447\u0435\u0440\u0435\u0437 \u0447\u0430\u0441", "2026-03-21T08:00:00Z", "datetime")
                ])));

        var items = await service.ExtractAsync(CreateWindow(message), CancellationToken.None);
        var meeting = Assert.Single(items, item => item.Kind == ExtractedItemKind.Meeting);

        Assert.Equal(new DateTimeOffset(2026, 03, 21, 08, 00, 00, TimeSpan.Zero), meeting.DueAt);
        Assert.Equal("$event-enriched-meeting-1", meeting.SourceEventId);
    }

    [Fact]
    public async Task HeuristicExtraction_RecoversMeetingFromTemporalEnrichment_ParsesLocalDatetimeWithBusinessTimezone()
    {
        var sentAt = new DateTimeOffset(2026, 03, 21, 06, 53, 55, TimeSpan.Zero);
        var message = new NormalizedMessage(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "telegram",
            "!friends:matrix.localhost",
            "$event-enriched-meeting-local-time",
            "You",
            "\u0441\u043e\u0431\u0435\u0441\u0435\u0434 \u043f\u043e\u0441\u043b\u0435 \u043e\u0431\u0435\u0434\u0430",
            sentAt,
            sentAt,
            false);

        var service = CreateHeuristicService(new FakeTextEnrichmentClient(
            new TextEnrichmentResponse(
                null,
                null,
                [],
                [
                    new TextEnrichmentTemporalExpression("\u043f\u043e\u0441\u043b\u0435 \u043e\u0431\u0435\u0434\u0430", "2026-03-21T11:00:00", "datetime")
                ])));

        var items = await service.ExtractAsync(CreateWindow(message), CancellationToken.None);
        var meeting = Assert.Single(items, item => item.Kind == ExtractedItemKind.Meeting);

        Assert.Equal(new DateTimeOffset(2026, 03, 21, 08, 00, 00, TimeSpan.Zero), meeting.DueAt);
        Assert.Equal("$event-enriched-meeting-local-time", meeting.SourceEventId);
    }

    [Fact]
    public async Task HeuristicExtraction_RecoversMeetingFromTemporalEnrichment_AnchorsToCueMessage()
    {
        var userId = Guid.NewGuid();
        var first = new NormalizedMessage(
            Guid.NewGuid(),
            userId,
            "telegram",
            "!friends:matrix.localhost",
            "$event-enriched-meeting-cue",
            "You",
            "\u0441\u043e\u0431\u0435\u0441\u0435\u0434 \u0447\u0435\u0440\u0435\u0437 \u0447\u0430\u0441",
            new DateTimeOffset(2026, 03, 21, 06, 53, 55, TimeSpan.Zero),
            new DateTimeOffset(2026, 03, 21, 06, 53, 55, TimeSpan.Zero),
            false);
        var second = new NormalizedMessage(
            Guid.NewGuid(),
            userId,
            "telegram",
            "!friends:matrix.localhost",
            "$event-enriched-meeting-ack",
            "Peer",
            "\u043e\u043a",
            new DateTimeOffset(2026, 03, 21, 06, 54, 30, TimeSpan.Zero),
            new DateTimeOffset(2026, 03, 21, 06, 54, 30, TimeSpan.Zero),
            false);

        var service = CreateHeuristicService(new FakeTextEnrichmentClient((text, _, _) =>
        {
            if (!text.Contains("\u0441\u043e\u0431\u0435\u0441\u0435\u0434", StringComparison.OrdinalIgnoreCase))
            {
                return new TextEnrichmentResponse(null, null, [], []);
            }

            return new TextEnrichmentResponse(
                null,
                null,
                [],
                [
                    new TextEnrichmentTemporalExpression("\u0447\u0435\u0440\u0435\u0437 \u0447\u0430\u0441", "2026-03-21T08:00:00Z", "datetime")
                ]);
        }));

        var items = await service.ExtractAsync(CreateWindow(first, second), CancellationToken.None);
        var meeting = Assert.Single(items, item => item.Kind == ExtractedItemKind.Meeting);

        Assert.Equal("$event-enriched-meeting-cue", meeting.SourceEventId);
        Assert.Equal("\u0441\u043e\u0431\u0435\u0441\u0435\u0434 \u0447\u0435\u0440\u0435\u0437 \u0447\u0430\u0441", meeting.Summary);
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
    public async Task HeuristicExtraction_UsesLatestRescheduleFromDialogueWindow()
    {
        var userId = Guid.NewGuid();
        var first = new NormalizedMessage(
            Guid.NewGuid(),
            userId,
            "telegram",
            "!friends:matrix.localhost",
            "$event-window-meeting-1",
            "glebov84",
            "назначаю встречу сегодня в 20:00 по мск",
            new DateTimeOffset(2026, 03, 13, 09, 00, 00, TimeSpan.Zero),
            new DateTimeOffset(2026, 03, 13, 09, 00, 00, TimeSpan.Zero),
            false);
        var second = new NormalizedMessage(
            Guid.NewGuid(),
            userId,
            "telegram",
            "!friends:matrix.localhost",
            "$event-window-meeting-2",
            "Stas (Telegram)",
            "в 20 не могу давай завтра в 19",
            new DateTimeOffset(2026, 03, 13, 09, 01, 00, TimeSpan.Zero),
            new DateTimeOffset(2026, 03, 13, 09, 01, 00, TimeSpan.Zero),
            false);
        var third = new NormalizedMessage(
            Guid.NewGuid(),
            userId,
            "telegram",
            "!friends:matrix.localhost",
            "$event-window-meeting-3",
            "glebov84",
            "хорошо",
            new DateTimeOffset(2026, 03, 13, 09, 02, 00, TimeSpan.Zero),
            new DateTimeOffset(2026, 03, 13, 09, 02, 00, TimeSpan.Zero),
            false);

        var service = CreateHeuristicService();
        var items = await service.ExtractAsync(CreateWindow(first, second, third), CancellationToken.None);
        var meeting = Assert.Single(items, item => item.Kind == ExtractedItemKind.Meeting);

        Assert.Equal("в 20 не могу давай завтра в 19", meeting.Summary);
        Assert.Equal(new DateTimeOffset(2026, 03, 14, 16, 00, 00, TimeSpan.Zero), meeting.DueAt);
        Assert.Equal("$event-window-meeting-3", meeting.SourceEventId);
    }

    [Fact]
    public void MeetingSignalDetector_RecognizesRescheduleFromChunkFollowUp()
    {
        var observedAt = new DateTimeOffset(2026, 03, 13, 09, 15, 00, TimeSpan.Zero);
        var chunkText = """
            glebov84: назначаю встречу сегодня в 20:00 по мск
            Stas (Telegram): в 20 не могу давай завтра в 19
            glebov84: хорошо
            """;

        var signal = MeetingSignalDetector.TryFromChunk(
            chunkText,
            observedAt,
            observedAt,
            TimeZoneInfo.FindSystemTimeZoneById("Europe/Moscow"));

        Assert.NotNull(signal);
        Assert.Equal("в 20 не могу давай завтра в 19", signal!.Summary);
        Assert.Equal(new DateTimeOffset(2026, 03, 14, 16, 00, 00, TimeSpan.Zero), signal.ScheduledFor);
    }

    [Fact]
    public void MeetingSignalDetector_RecognizesInterviewKeywordWithExplicitTime()
    {
        var observedAt = new DateTimeOffset(2026, 03, 21, 06, 53, 55, TimeSpan.Zero);
        var chunkText = "You: \u0441\u0435\u0433\u043e\u0434\u043d\u044f \u043f\u0440\u043e\u0432\u0435\u0434\u0435\u043c \u0441\u043e\u0431\u0435\u0441\u0435\u0434\u043e\u0432\u0430\u043d\u0438\u0435 \u0432 11:00 \u0431\u0443\u0434\u044c \u043d\u0430 \u0433\u043e\u0442\u043e\u0432\u0435";

        var signal = MeetingSignalDetector.TryFromChunk(
            chunkText,
            observedAt,
            observedAt,
            TimeZoneInfo.FindSystemTimeZoneById("Europe/Moscow"));

        Assert.NotNull(signal);
        Assert.Equal(new DateTimeOffset(2026, 03, 21, 08, 00, 00, TimeSpan.Zero), signal!.ScheduledFor);
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

        var candidate = chunk.ToMeetingCandidate(
            TimeZoneInfo.FindSystemTimeZoneById("Europe/Moscow"));

        Assert.Null(candidate);
    }

    [Fact]
    public void DigestComposer_BuildMeetings_ReturnsNearestUpcomingMeetings()
    {
        var now = new DateTimeOffset(2026, 03, 13, 10, 00, 00, TimeSpan.FromHours(6));
        var meetings = new[]
        {
            new MeetingRecord(Guid.NewGuid(), Guid.NewGuid(), "Upcoming meeting", "Созвон в 11", "!team", "$1", null, now.AddMinutes(-5), now.AddHours(1), new Confidence(0.8)),
            new MeetingRecord(Guid.NewGuid(), Guid.NewGuid(), "Upcoming meeting", "Встреча завтра в 9", "!team", "$2", null, now, now.AddDays(1).AddHours(-1), new Confidence(0.7)),
            new MeetingRecord(Guid.NewGuid(), Guid.NewGuid(), "Upcoming meeting", "Очень дальняя встреча", "!team", "$3", null, now, now.AddDays(30), new Confidence(0.9))
        };

        var upcoming = DigestComposer.BuildMeetings(meetings, now);

        Assert.Equal(2, upcoming.Count);
        Assert.Equal("Созвон в 11", upcoming[0].Summary);
        Assert.Equal("Встреча завтра в 9", upcoming[1].Summary);
    }

    private static HeuristicStructuredExtractionService CreateHeuristicService(ITextEnrichmentClient? textEnrichmentClient = null)
    {
        return new HeuristicStructuredExtractionService(
            new PilotOptions
            {
                TodayTimeZoneId = "Europe/Moscow"
            },
            textEnrichmentClient ?? new FakeTextEnrichmentClient((TextEnrichmentResponse?)null));
    }

    private static DeepSeekStructuredExtractionService CreateDeepSeekService(
        IDeepSeekJsonClient client,
        ILogger<DeepSeekStructuredExtractionService>? logger = null)
    {
        return new DeepSeekStructuredExtractionService(
            client,
            CreateHeuristicService(),
            new PilotOptions
            {
                TodayTimeZoneId = "Europe/Moscow"
            },
            logger ?? NullLogger<DeepSeekStructuredExtractionService>.Instance);
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

    private sealed class FakeTextEnrichmentClient : ITextEnrichmentClient
    {
        private readonly TextEnrichmentResponse? response;
        private readonly Func<string, DateTimeOffset, string, TextEnrichmentResponse?>? resolver;

        public FakeTextEnrichmentClient(TextEnrichmentResponse? response)
        {
            this.response = response;
        }

        public FakeTextEnrichmentClient(Func<string, DateTimeOffset, string, TextEnrichmentResponse?> resolver)
        {
            this.resolver = resolver;
        }

        public bool IsConfigured => response is not null || resolver is not null;

        public Task<TextEnrichmentResponse?> EnrichAsync(
            string text,
            DateTimeOffset referenceTimeUtc,
            string timeZoneId,
            CancellationToken cancellationToken)
        {
            if (resolver is not null)
            {
                return Task.FromResult(resolver(text, referenceTimeUtc, timeZoneId));
            }

            return Task.FromResult(response);
        }
    }

    private sealed class RecordingLogger<T> : ILogger<T>
    {
        public List<string> Messages { get; } = [];

        public IDisposable BeginScope<TState>(TState state) where TState : notnull
        {
            return NullScope.Instance;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Messages.Add(formatter(state, exception));
        }

        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new();

            public void Dispose()
            {
            }
        }
    }
}
