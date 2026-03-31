# Архитектурные улучшения Super Chat Pipeline

> Анализ текущего message processing pipeline с конкретными предложениями по улучшению. Каждое предложение содержит: проблему, почему она важна, как выглядит сейчас, и как должно выглядеть после.

---

## Карта текущего pipeline

```
Matrix Synapse (/sync каждые 4 сек)
    │
    ▼
MatrixSyncBackgroundService          ← INGESTION (последовательно по пользователям)
    │  для каждого event:
    │  - фильтрация комнат (HTTP к telegram-helper)
    │  - фильтрация сообщений (regex/rules)
    ▼
MessageNormalizationService          ← PERSIST + DISPATCH (per-message)
    │  - dedup (SELECT EXISTS)
    │  - INSERT normalized_message
    │  - dispatch 2 Rebus команды
    ├─── DEFER(20s) ──────────┐
    │                         ▼
    │            ProcessConversationAfterSettleCmd
    │              - собрать окна диалога
    │              - AI extraction (DeepSeek LLM)
    │              - сохранить work items / meetings
    │              - dispatch resolution commands
    │
    └─── SEND(immediate) ────┐
                              ▼
                 RebuildConversationChunksCmd
                   - перестроить чанки комнаты
                   ├─→ IndexConversationChunksCmd (embed + Qdrant)
                   └─→ ProjectConversationMeetingsCmd (detect meetings)
```

---

## 1. Усиление команд без дедупликации

### Проблема

Когда в одной комнате за 4 секунды приходит 10 сообщений, каждое из них отправляет 2 Rebus-команды. Итого:
- 10 × `RebuildConversationChunksCommand` для одной и той же комнаты
- 10 × `ProcessConversationAfterSettleCommand` для одной и той же комнаты

Каждый `RebuildConversationChunksCommand` загружает ВСЕ сообщения комнаты, удаляет перекрывающиеся чанки и строит заново. 10 сообщений = 10 полных перестроений одних и тех же чанков. Это O(N²) по количеству сообщений в burst.

### Сейчас (MessageNormalizationService → RebusPipelineCommandScheduler)

```
сообщение #1 → INSERT → SEND RebuildChunks(room=X) + DEFER ProcessConversation(room=X)
сообщение #2 → INSERT → SEND RebuildChunks(room=X) + DEFER ProcessConversation(room=X)
...
сообщение #10 → INSERT → SEND RebuildChunks(room=X) + DEFER ProcessConversation(room=X)

Результат: 10 идентичных перестроений чанков, 10 загрузок pending messages
```

### Улучшенный вариант: batch dispatch

```csharp
// Вместо per-message dispatch:
// Собрать все сообщения за sync tick в batch, dispatch один раз per (userId, roomId)

// В MatrixSyncBackgroundService:
var storedMessages = new List<(Guid UserId, string RoomId, DateTimeOffset SentAt)>();

foreach (var event in acceptedEvents)
{
    if (await normalizationService.TryStoreAsync(...)) // persist only, no dispatch
        storedMessages.Add((target.UserId, room.RoomId, event.SentAt));
}

// После всех сообщений пользователя — один dispatch per room
var roomGroups = storedMessages.GroupBy(m => m.RoomId);
foreach (var group in roomGroups)
{
    var earliest = group.Min(m => m.SentAt);
    await pipelineCommandScheduler.DispatchBatchAsync(userId, group.Key, earliest, ct);
}
```

**Эффект:** 10 сообщений → 1 rebuild + 1 settle вместо 10 + 10. Экономия ~90% DB и CPU нагрузки на bursts.

**Сложность:** Средняя. Нужно разделить persist и dispatch в MessageNormalizationService.

---

## 2. Последовательные HTTP-вызовы для room info (N+1)

### Проблема

Для каждой комнаты в sync batch вызывается `telegramRoomInfoService.GetRoomInfoAsync()` — HTTP к Python sidecar. Если у пользователя 30 комнат, это 30 последовательных HTTP запросов. При latency 50ms на запрос — 1.5 секунды только на room info, из 4 секунд бюджета sync tick.

### Сейчас (MatrixSyncBackgroundService:184-200)

```csharp
foreach (var room in result.Rooms)
{
    // ...
    var telegramRoomInfo = await telegramRoomInfoService.GetRoomInfoAsync(
        target.MatrixUserId, room.RoomId, cancellationToken);  // HTTP call #N

    if (!ShouldIngestRoom(..., telegramRoomInfo, ...))
        continue;
}
```

### Улучшенный вариант: параллельные запросы

```csharp
// Собрать все roomId, запросить параллельно
var nonManagementRooms = result.Rooms
    .Where(r => !IsManagementRoom(r.RoomId, target.ManagementRoomId))
    .ToList();

var roomInfoTasks = nonManagementRooms.ToDictionary(
    r => r.RoomId,
    r => telegramRoomInfoService.GetRoomInfoAsync(target.MatrixUserId, r.RoomId, ct));

await Task.WhenAll(roomInfoTasks.Values);

foreach (var room in nonManagementRooms)
{
    var roomInfo = roomInfoTasks[room.RoomId].Result; // already completed
    if (!ShouldIngestRoom(..., roomInfo, ...))
        continue;
    // process messages
}
```

**Эффект:** 30 комнат за ~50ms вместо ~1500ms. Sync tick укладывается в бюджет.

**Ещё лучше (batch endpoint):** Добавить в telegram-helper `POST /rooms/batch-info` для одного HTTP-запроса на все комнаты.

---

## 3. Per-message DB round-trips при сохранении

### Проблема

Каждое сообщение из sync проходит через `TryStoreAsync` отдельно:
1. `SELECT EXISTS` — проверка дубликата (1 DB round-trip)
2. `INSERT` + `SaveChangesAsync` (1 DB round-trip)
3. Rebus outbox dispatch (1 DB round-trip если transactional)

20 сообщений за tick = 40-60 sequential DB round-trips.

### Сейчас (MessageNormalizationService.TryStoreAsync)

```csharp
// Вызывается 20 раз за tick, каждый раз новый DbContext
var exists = await dbContext.NormalizedMessages.AnyAsync(
    item => item.UserId == userId && item.MatrixRoomId == roomId && item.MatrixEventId == eventId);
if (exists) return false;

dbContext.NormalizedMessages.Add(entity);
await dbContext.SaveChangesAsync();      // round-trip #2
await dispatcher.DispatchAsync(...);     // round-trip #3
```

### Улучшенный вариант: batch insert

```csharp
// Новый метод: TryStoreBatchAsync
public async Task<int> TryStoreBatchAsync(
    Guid userId, IReadOnlyList<IncomingMessage> messages, CancellationToken ct)
{
    await using var dbContext = await dbContextFactory.CreateDbContextAsync(ct);

    // Один запрос: найти уже существующие eventId
    var eventIds = messages.Select(m => m.EventId).ToList();
    var existing = await dbContext.NormalizedMessages
        .Where(n => n.UserId == userId && eventIds.Contains(n.MatrixEventId))
        .Select(n => n.MatrixEventId)
        .ToHashSetAsync(ct);

    // Batch insert новых
    var newEntities = messages
        .Where(m => !existing.Contains(m.EventId))
        .Select(m => CreateEntity(userId, m))
        .ToList();

    if (newEntities.Count == 0) return 0;

    dbContext.NormalizedMessages.AddRange(newEntities);
    await dbContext.SaveChangesAsync(ct);  // один round-trip для всех
    return newEntities.Count;
}
```

**Эффект:** 20 сообщений → 2 DB round-trips (1 SELECT + 1 batch INSERT) вместо 40-60.

---

## 4. Потеря сообщений при ошибке room info

### Проблема (КРИТИЧЕСКАЯ)

Если HTTP-запрос к telegram-helper за room info падает (сетевой сбой, таймаут), сообщения этой комнаты пропускаются. Но sync batch token **всё равно обновляется**. Matrix /sync не вернёт эти сообщения повторно — они потеряны навсегда.

### Сейчас (MatrixSyncBackgroundService:188-200)

```csharp
try
{
    telegramRoomInfo = await telegramRoomInfoService.GetRoomInfoAsync(...);
}
catch (Exception ex)
{
    logger.LogWarning(ex, "Failed to get room info for {RoomId}.", room.RoomId);
    // telegramRoomInfo остаётся null
    // ShouldIngestRoom может пропустить комнату
    // batch token обновится в PersistSyncStateAsync
    // сообщения ПОТЕРЯНЫ
}
```

### Улучшенный вариант: fail-safe ingestion

```csharp
try
{
    telegramRoomInfo = await telegramRoomInfoService.GetRoomInfoAsync(...);
}
catch (Exception ex)
{
    logger.LogWarning(ex, "Failed to get room info for {RoomId}. Ingesting with default policy.", room.RoomId);
    // Вместо пропуска — инжестить с conservative defaults
    // Лучше инжестить лишнее сообщение, чем потерять важное
    telegramRoomInfo = TelegramRoomInfo.UnknownFallback;
}

// В ShouldIngestRoom: unknown rooms инжестятся с пониженным приоритетом,
// но не пропускаются
```

**Альтернатива:** Не обновлять batch token для пользователя, если хотя бы одна комната имела ошибку room info. Тогда на следующем tick те же сообщения будут обработаны (dedup в TryStoreAsync отсечёт уже сохранённые).

---

## 5. DeepSeek silent failure глушит весь AI resolution

### Проблема (КРИТИЧЕСКАЯ)

`DeepSeekResolutionService.ResolveAsync` ловит `catch (Exception)` и возвращает пустой массив. Это значит:
- `OperationCanceledException` (cancellation) — проглочена
- `NullReferenceException` (баг в коде) — проглочен
- `OutOfMemoryException` — проглочено

Для пользователя: AI resolution тихо перестаёт работать. Work items, которые должны автоматически закрываться — висят вечно.

### Сейчас (DeepSeekResolutionService:48-52)

```csharp
catch (Exception exception)
{
    logger.LogWarning(exception, "AI resolution failed...");
    return Array.Empty<AiResolutionDecisionResult>();
}
```

### Улучшенный вариант

```csharp
catch (OperationCanceledException)
{
    throw; // cancellation MUST propagate
}
catch (HttpRequestException ex)
{
    logger.LogWarning(ex, "DeepSeek API call failed for {Count} candidates.", candidates.Count);
    return Array.Empty<AiResolutionDecisionResult>();
}
catch (JsonException ex)
{
    logger.LogWarning(ex, "DeepSeek returned unparseable response for {Count} candidates.", candidates.Count);
    return Array.Empty<AiResolutionDecisionResult>();
}
catch (Exception ex)
{
    logger.LogError(ex, "Unexpected error in AI resolution for {Count} candidates.", candidates.Count);
    throw; // programming bugs must NOT be silenced
}
```

**Эффект:** Transient ошибки (HTTP, JSON) деградируют gracefully. Bugs и cancellation — пробрасываются.

---

## 6. Unbounded queries загружают всю историю в память

### Проблема

`WorkItemAutoResolutionService` и `MeetingAutoResolutionService` загружают ВСЕ сообщения пользователя в комнате без временного фильтра на уровне БД. Фильтр по времени применяется потом в памяти.

Пользователь с 100K сообщений в активных комнатах → 100K строк в RAM → O(n) per candidate.

### Сейчас (WorkItemAutoResolutionService:58-62)

```csharp
var messages = await dbContext.NormalizedMessages
    .AsNoTracking()
    .Where(item => item.UserId == userId &&
                   roomIds.Contains(item.MatrixRoomId))
    // НЕТ фильтра по времени на уровне SQL!
    .ToListAsync(cancellationToken);

// Фильтр потом в памяти:
var laterMessages = messages.Where(m => m.SentAt >= observedFrom);
```

### Улучшенный вариант

```csharp
// Вычислить самую раннюю границу из всех candidates
var earliestObserved = candidates.Min(c => c.ObservedAt);

var messages = await dbContext.NormalizedMessages
    .AsNoTracking()
    .Where(item => item.UserId == userId &&
                   roomIds.Contains(item.MatrixRoomId) &&
                   item.SentAt >= earliestObserved)  // ← фильтр на уровне SQL
    .ToListAsync(cancellationToken);
```

**Эффект:** Вместо 100K строк загружаем только сообщения за последние N минут/часов. Размер ответа падает на порядки.

**То же самое** нужно в `ConversationResolutionService.LoadConversationCandidatesAsync` (строка 115) и `MeetingAutoResolutionService` (строка 85).

---

## 7. Последовательная синхронизация пользователей

### Проблема

Все пользователи обрабатываются последовательно в одном цикле. Если 10 пользователей, каждый sync занимает ~500ms (HTTP к Matrix), то весь tick = 5 секунд > 4 секунд бюджета. Sync начинает отставать.

### Сейчас (MatrixSyncBackgroundService:132-138)

```csharp
foreach (var target in syncTargets)
{
    var userResult = await SyncUserAsync(target, cancellationToken);
    // ...
}
```

### Улучшенный вариант: bounded parallelism

```csharp
await Parallel.ForEachAsync(
    syncTargets,
    new ParallelOptions { MaxDegreeOfParallelism = 4, CancellationToken = ct },
    async (target, ct) =>
    {
        var userResult = await SyncUserAsync(target, ct);
        // aggregate stats thread-safe
    });
```

**Эффект:** 10 пользователей с parallelism=4 → ~3 round-trips по ~500ms = 1.5 сек вместо 5 сек.

**Важно:** `SyncUserAsync` уже stateless per-user (нет shared mutable state между пользователями), так что параллелизм безопасен.

---

## 8. Отсутствие CancellationToken в Rebus handlers

### Проблема

Все Rebus command handlers передают `CancellationToken.None` в downstream сервисы. При graceful shutdown приложения:
- LLM вызовы (extraction, resolution) работают до таймаута
- Embedding HTTP вызовы не прерываются
- DB операции не отменяются

Результат: приложение зависает на shutdown.

### Сейчас (PipelineCommandHandlers — все Handle методы)

```csharp
public async Task Handle(ProcessConversationAfterSettleCommand message)
{
    // ...
    await extractionService.ExtractAsync(window, CancellationToken.None);  // не отменяется
    await workItemService.IngestRangeAsync(userId, items, CancellationToken.None);
}
```

### Улучшенный вариант

```csharp
// Инжектировать IHostApplicationLifetime
public sealed class ProcessConversationAfterSettleCommandHandler(
    ...,
    IHostApplicationLifetime lifetime) : IHandleMessages<ProcessConversationAfterSettleCommand>
{
    public async Task Handle(ProcessConversationAfterSettleCommand message)
    {
        var ct = lifetime.ApplicationStopping;
        await extractionService.ExtractAsync(window, ct);
        await workItemService.IngestRangeAsync(userId, items, ct);
    }
}
```

**Эффект:** Graceful shutdown за секунды вместо минут.

---

## 9. Дублирование загрузки messages в resolution pipeline

### Проблема

При resolution одного conversation `normalized_messages` загружаются 3 раза:
1. `ConversationResolutionService.LoadConversationCandidatesAsync` — для построения candidates
2. `WorkItemAutoResolutionService.ResolveCoreAsync` — для heuristic resolution
3. `MeetingAutoResolutionService.ResolveCoreAsync` — для meeting resolution

Плюс создаётся 4 отдельных DbContext.

### Сейчас

```
ResolveConversationAsync()
    ├─ DbContext #1: LoadCandidates → SELECT messages WHERE room=X
    ├─ DbContext #2: ApplyAiDecisions → SELECT work_items + meetings
    ├─ DbContext #3: WorkItemAutoResolution → SELECT messages WHERE room=X (повтор!)
    └─ DbContext #4: MeetingAutoResolution → SELECT messages WHERE room=X (повтор!)
```

### Улучшенный вариант: shared message context

```csharp
public async Task ResolveConversationAsync(Guid userId, string roomId, DateTimeOffset now, CancellationToken ct)
{
    // Загрузить messages ОДИН раз
    await using var dbContext = await dbContextFactory.CreateDbContextAsync(ct);
    var messages = await LoadMessagesOnceAsync(dbContext, userId, roomId, ct);

    var candidates = BuildCandidates(dbContext, messages);
    var aiDecisions = await aiResolutionService.ResolveAsync(candidates, ct);

    ApplyAiDecisions(dbContext, aiDecisions);
    workItemAutoResolutionService.Resolve(dbContext, candidates, messages); // передаём готовые данные
    meetingAutoResolutionService.Resolve(dbContext, candidates, messages);  // те же данные

    await dbContext.SaveChangesAsync(ct); // одна транзакция
}
```

**Эффект:** 1 DbContext и 1 загрузка messages вместо 4 + 3.

---

## 10. Sequential embedding в ChunkIndexingService

### Проблема

При индексации чанков каждый чанк embedded последовательно — один HTTP-вызов к Python sidecar за раз. 10 чанков = 10 sequential HTTP запросов.

### Сейчас (ChunkIndexingService:118)

```csharp
foreach (var chunk in chunks)
{
    var embedding = await embeddingService.EmbedAsync(chunk.Content, ct);  // HTTP #N
    points.Add(BuildPoint(chunk, embedding));
}
await qdrantClient.UpsertAsync(collectionName, points, ct);  // batch upsert (хорошо!)
```

### Улучшенный вариант: batch embedding

```csharp
// Если embedding sidecar поддерживает batch:
var contents = chunks.Select(c => c.Content).ToList();
var embeddings = await embeddingService.EmbedBatchAsync(contents, ct);  // 1 HTTP call

// Если не поддерживает batch — параллельные запросы:
var embeddingTasks = chunks.Select(c => embeddingService.EmbedAsync(c.Content, ct)).ToList();
var embeddings = await Task.WhenAll(embeddingTasks);
```

**Эффект:** 10 чанков за 1 HTTP-вызов (batch) или ~100ms (parallel) вместо ~1000ms (sequential).

---

## 11. Heuristic extraction смешивает detection и enrichment

### Проблема

`HeuristicStructuredExtractionService` (542 строки) делает две разных вещи:
1. **Detection** — вызывает `HeuristicSignalDetector.Detect()` (чистая domain логика)
2. **Enrichment** — вызывает `ITextEnrichmentClient` для NER, temporal parsing, meeting recovery

Это разные стадии pipeline, которые нельзя тестировать и менять независимо.

### Сейчас

```
HeuristicStructuredExtractionService.ExtractAsync()
    ├─ HeuristicSignalDetector.Detect()     ← domain (чистая функция)
    ├─ RecoverMeetingFromTemporalEnrichment  ← HTTP к Python sidecar
    ├─ EnrichItemsAsync                      ← HTTP к Python sidecar
    └─ ApplyWaitingOnWindowRules             ← domain (чистая функция)
```

### Улучшенный вариант: разделение стадий

```
// Стадия 1: Detection (чистая domain, без I/O)
ISignalDetectionService.DetectAsync(window)
    └─ HeuristicSignalDetector.Detect()
    └─ ApplyWaitingOnWindowRules()

// Стадия 2: Enrichment (I/O к text-enrichment sidecar)
IExtractionEnrichmentService.EnrichAsync(items, window)
    └─ RecoverMeetingFromTemporalEnrichment
    └─ EnrichItemsAsync (person, deadline resolution)

// Оркестратор остаётся тонким:
public async Task<List<ExtractedItem>> ExtractAsync(ConversationWindow window, CancellationToken ct)
{
    var items = signalDetectionService.Detect(window);
    return await enrichmentService.EnrichAsync(items, window, ct);
}
```

**Эффект:** Detection тестируется без mock HTTP. Enrichment можно отключить/заменить независимо.

---

## 12. DeepSeek → Heuristic fallback зашит в concrete class

### Проблема

`DeepSeekStructuredExtractionService` зависит от `HeuristicStructuredExtractionService` как **concrete class** (не интерфейс). Fallback-стратегия захардкожена. Нельзя:
- Заменить fallback на другой provider
- Добавить третий extraction backend
- Тестировать DeepSeek без real HeuristicService

### Сейчас

```csharp
public sealed class DeepSeekStructuredExtractionService(
    HeuristicStructuredExtractionService heuristicService,  // ← concrete class!
    ...) : IAiStructuredExtractionService
```

### Улучшенный вариант: strategy через DI

```csharp
// Зарегистрировать fallback как keyed service
services.AddKeyedSingleton<IAiStructuredExtractionService, HeuristicStructuredExtractionService>("fallback");

public sealed class DeepSeekStructuredExtractionService(
    [FromKeyedServices("fallback")] IAiStructuredExtractionService fallbackService,
    ...) : IAiStructuredExtractionService
```

---

## 13. Dual meeting creation без cross-deduplication

### Проблема

Meetings создаются двумя независимыми путями:
1. **Extraction path** → `WorkItemIngestionService` → meetings table (SourceEventId = Matrix event ID)
2. **Chunk projection path** → `MeetingProjectionService` → meetings table (SourceEventId = `chunk:*`)

Одна и та же реальная встреча может появиться дважды из разных детекторов. Дедупликации между путями нет.

### Решение

Добавить cross-path deduplication в `MeetingProjectionService`:

```csharp
// При проецировании из чанка, проверить нет ли уже meeting
// из extraction path с тем же scheduled_for ± 30 минут в той же комнате
var existingExtractionMeetings = await dbContext.Meetings
    .Where(m => m.UserId == userId && m.SourceRoom == roomId
        && !m.SourceEventId.StartsWith("chunk:")
        && m.ScheduledFor != null)
    .ToListAsync(ct);

// Пропустить chunk-meeting если extraction-meeting уже покрывает этот слот
```

---

## Приоритеты реализации

### Немедленно (баги и потери данных)

| # | Проблема | Эффект | Время |
|---|----------|--------|-------|
| 4 | Потеря сообщений при ошибке room info | Данные перестанут теряться | 30 мин |
| 5 | DeepSeek silent failure | AI resolution перестанет тихо ломаться | 30 мин |

### Высокий приоритет (производительность)

| # | Проблема | Эффект | Время |
|---|----------|--------|-------|
| 1 | Command amplification (10x rebuild per burst) | -90% нагрузки на bursts | 2-3 часа |
| 2 | Sequential room info HTTP (N+1) | Sync tick в 10x быстрее | 1 час |
| 6 | Unbounded message queries | Память не растёт с историей | 30 мин |
| 3 | Per-message DB round-trips | -80% DB round-trips | 2 часа |

### Средний приоритет (масштабируемость)

| # | Проблема | Эффект | Время |
|---|----------|--------|-------|
| 7 | Sequential user sync | Поддержка >5 пользователей | 1 час |
| 10 | Sequential embedding | Indexing в 5-10x быстрее | 1 час |
| 9 | Triple message loading in resolution | -66% DB load в resolution | 2 часа |
| 8 | CancellationToken.None | Graceful shutdown | 1 час |

### При следующем касании (архитектура)

| # | Проблема | Эффект | Время |
|---|----------|--------|-------|
| 11 | Detection + enrichment в одном классе | Testability, replaceability | 2 часа |
| 12 | Concrete fallback dependency | Extensibility | 30 мин |
| 13 | Dual meeting creation | Нет дублей в UI | 1 час |

---

*Документ составлен 30.03.2026 на основе анализа pipeline агентами Plan, Silent-failure-hunter и Type-design-analyzer.*
