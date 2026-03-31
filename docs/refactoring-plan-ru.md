# План рефакторинга Super Chat

> Документ описывает проблемы в кодовой базе простым языком и предлагает конкретные шаги по исправлению. Каждая проблема объясняется через аналогию: почему так плохо и что будет если не починить.

---

## Оглавление

1. [Обзор: что не так в целом](#1-обзор-что-не-так-в-целом)
2. [Критические классы: разбить обязательно](#2-критические-классы-разбить-обязательно)
3. [Средние проблемы: разбить при следующем касании](#3-средние-проблемы-разбить-при-следующем-касании)
4. [Дублирование кода](#4-дублирование-кода)
5. [DbContext и entity-классы](#5-dbcontext-и-entity-классы)
6. [Порядок работы](#6-порядок-работы)

---

## 1. Обзор: что не так в целом

### Проблема одним предложением

В проекте есть 3-4 класса, которые делают по 5-6 разных вещей одновременно. Когда класс отвечает за всё — его невозможно тестировать по частям, в нём страшно менять что-то одно (потому что можешь сломать другое), и новый разработчик не может понять что он делает без прочтения 700 строк.

### Аналогия

Представь, что один человек в компании одновременно отвечает на звонки, ведёт бухгалтерию, чинит компьютеры, управляет складом и ещё подвозит сотрудников. Если он заболеет — встанет всё. Если ему нужно поменять процесс на складе — он может случайно сломать бухгалтерию. Решение: разделить роли. Каждый делает одну вещь, но делает её хорошо.

### Масштаб проблемы

| Категория | Сколько | Пример |
|-----------|---------|--------|
| Классы >400 строк с нарушением SRP | 3 | MatrixSyncBackgroundService, ChatExperienceService, HeuristicStructuredExtractionService |
| Классы 300-400 строк, пограничные | 5 | ChunkBuilderService, ConversationResolutionService, MeetingProjectionService, MatrixApiClient, ServiceCollectionExtensions |
| Дублирование кода | 6 случаев | RecordingHandler (6 копий), TimeZone resolve (2 реализации, одна с пустыми catch) |
| Классы в хорошем состоянии | ~30+ | SearchService (59 строк), DigestService (62 строки), все API endpoints |

### Что НЕ нужно трогать

- **SearchService, DigestService** — образцовые тонкие оркестраторы, 59-62 строки
- **API endpoints** — отлично структурированы, 25-45 строк каждый
- **10 из 14 PageModel** — тонкие, 10-84 строки
- **AiPipelineLog** (407 строк) — это auto-generated LoggerMessage, так и должно быть
- **LegacyDatabaseMigrationBootstrapper** (410 строк) — одноразовый код миграции, трогать опасно
- **Domain детекторы** (MeetingSignalDetector, HeuristicSignalDetector) — статические классы с чистой логикой, 297-369 строк допустимо

---

## 2. Критические классы: разбить обязательно

### 2.1 MatrixSyncBackgroundService — 706 строк, 10 зависимостей

**Файл:** `src/SuperChat.Infrastructure/Features/Operations/MatrixSyncBackgroundService.cs`

#### Что он делает сейчас (6 разных вещей)

1. **Крутит фоновый цикл** каждые 4 секунды, считает метрики Prometheus
2. **Инжектит тестовые данные** при DevSeedSampleData=true (hard-coded sample messages)
3. **Загружает Matrix events** через /sync API для каждого пользователя
4. **Парсит ответы Telegram bridge** — определяет логин, код, пароль, потерю соединения, приветствие бота
5. **Фильтрует комнаты** — определяет какие комнаты инжестить, какие пропустить, в какие вступить
6. **Сохраняет состояние** — checkpoint-ы, connection state, WebLoginUrl

#### Почему это плохо

- **10 зависимостей в конструкторе.** Для BackgroundService нормально 3-4. Десять — это сигнал, что класс пытается делать слишком много.
- **SyncUserAsync — 186 строк.** Один метод, который нельзя протестировать по частям. Если упадёт парсинг bridge-сообщения — сломается вся синхронизация.
- **Хорошая новость: 10 из 19 методов уже `internal static`.** Это чистые функции без побочных эффектов. Их можно вынести механически — просто переместить в другой класс.

#### Как разбить — 5 новых классов

```
БЫЛО:
  MatrixSyncBackgroundService (706 строк, 19 методов, 10 зависимостей)

СТАНЕТ:
  MatrixSyncBackgroundService     (~80 строк)  — только цикл, метрики, выбор dev/prod режима
  MatrixSyncOrchestrator          (~200 строк) — загрузка targets, итерация по пользователям, persist state
  BridgeMessageInterpreter        (~100 строк) — 6 static-методов парсинга ответов бриджа
  RoomIngestionPolicy             (~70 строк)  — 3 static-метода фильтрации комнат
  MatrixSenderResolver            (~60 строк)  — резолв sender info + DeriveSenderName
  DevelopmentSeedService          (~80 строк)  — SeedSampleMessagesAsync + RunDevelopmentSeedAsync
```

#### Какие методы куда идут

| Новый класс | Методы |
|-------------|--------|
| **BridgeMessageInterpreter** | `LooksLikeSuccessfulLogin`, `LooksLikeBridgeGreeting`, `LooksLikeLostConnection`, `DetectLoginStep`, `ShouldRetryBridgeLogin`, `ResolveConnectionStateAfterSuccessfulSync` |
| **RoomIngestionPolicy** | `ShouldIngestRoom`, `IsManagementRoom`, `GetInvitedRoomsToJoin`, `ShouldIngestMessageBody` |
| **MatrixSenderResolver** | `ResolveSenderInfoAsync`, `DeriveSenderName` |
| **DevelopmentSeedService** | `RunDevelopmentSeedAsync`, `SeedSampleMessagesAsync` |
| **MatrixSyncOrchestrator** | `RunRealSyncAsync`, `SyncUserAsync`, `PersistSyncStateAsync`, `UpdateConnectionStateAsync` |
| **MatrixSyncBackgroundService** | `ExecuteAsync` (вызывает Orchestrator или DevelopmentSeedService) |

#### Сложность и риски

- **Сложность: средняя.** Большинство методов static — просто copy-paste в новый файл.
- **Риск: низкий.** Тесты уже покрывают static-методы напрямую (они internal). После переноса тесты продолжат работать, нужно только обновить namespace.
- **Время: ~2-3 часа.**

---

### 2.2 ChatExperienceService — 473 строки, 7 зависимостей

**Файл:** `src/SuperChat.Infrastructure/Features/Chat/ChatExperienceService.cs`

#### Что он делает сейчас (5 разных вещей)

1. **Маршрутизирует вопрос** — определяет это template или custom, по ключевым словам находит нужный шаблон
2. **Template pipeline** — вызывает handler, получает ответ, потом "улучшает" его через LLM
3. **Custom pipeline** — семантический поиск (Qdrant), генерация ответа через DeepSeek
4. **Поиск с fallback** — если retrieval не дал результатов, пробует полнотекстовый поиск, потом по отдельным словам
5. **NLP утилиты** — стоп-слова, определение "пустого" ответа LLM, извлечение title/summary из текста

#### Почему это плохо

- **Один метод `AskAsync` скрывает два совершенно разных pipeline.** Template и Custom — это разные алгоритмы с разными зависимостями. Они никогда не выполняются одновременно.
- **`TryEnhanceTemplateAnswerAsync` — 99 строк.** Самый сложный метод, который берёт шаблонный ответ и "дополняет" его через AI. Если захочешь поменять промпт — придётся разбираться в 473 строках.
- **Цепочка fallback-ов в `BuildCustomAnswerAsync`:** Retrieve → Generate → если "пустой" → Search → Generate again. Каждый шаг по-хорошему — отдельная стратегия.

#### Как разбить — 4 класса

```
БЫЛО:
  ChatExperienceService (473 строк, 15 методов, 7 зависимостей)

СТАНЕТ:
  ChatExperienceService       (~60 строк)  — только AskAsync: валидация, dispatch
  TemplateAnswerEnhancer      (~120 строк) — TryEnhanceTemplateAnswerAsync + BuildContextText + LooksLikeNoContextAnswer
  CustomChatPipeline          (~150 строк) — BuildCustomAnswerAsync + RetrieveSmartAsync + SearchSmartAsync
  ChatQuestionRouter          (~50 строк)  — DetectTemplateFromQuestion + ContainsAny + StopWords
```

#### Какие методы куда идут

| Новый класс | Методы |
|-------------|--------|
| **ChatExperienceService** | `AskAsync`, `DispatchToTemplateAsync`, `BuildTemplateAnswerAsync` |
| **TemplateAnswerEnhancer** | `TryEnhanceTemplateAnswerAsync`, `BuildContextText`, `LooksLikeNoContextAnswer`, `BuildRetrievedTitle`, `BuildRetrievedSummary` |
| **CustomChatPipeline** | `BuildCustomAnswerAsync`, `RetrieveSmartAsync`, `SearchSmartAsync`, `BuildSearchCandidates` |
| **ChatQuestionRouter** | `DetectTemplateFromQuestion`, `ContainsAny`, `StopWords` |

#### Сложность и риски

- **Сложность: средняя.** Методы хорошо изолированы, зависимости чётко делятся.
- **Риск: низкий.** `AskAsync` — единственная публичная точка входа, вся перемотка внутренняя.
- **Время: ~2 часа.**

---

### 2.3 TodayModel (PageModel) — 272 строки, 7 зависимостей

**Файл:** `src/SuperChat.Web/Pages/Today.cshtml.cs`

#### Что он делает сейчас (4 разных вещи)

1. **Загружает 4 секции дайджеста** — waiting, meetings, today focus, commitments
2. **Выполняет команды** complete/dismiss через switch по WorkItemType к трём разным сервисам
3. **Определяет навигацию** — активная секция, редирект после действия
4. **Содержит ViewModel-ы** — 3 вложенных record: TodaySummary, TodaySection, TodayCard

#### Почему это плохо

- **7 DI-зависимостей** для PageModel — слишком много. Из них 3 — это однотипные command-сервисы для разных типов work items.
- **Dispatch по WorkItemType** (complete/dismiss) дублируется с API endpoint-ами — та же логика switch по типу.
- **ViewModel-ы внутри PageModel** увеличивают файл на 35 строк без добавления логики.

#### Как разбить — 3 части

```
БЫЛО:
  TodayModel (272 строки, 7 зависимостей)

СТАНЕТ:
  TodayModel                   (~120 строк, 4 зависимости) — загрузка, навигация
  IWorkItemCommandDispatcher   (~40 строк)  — dispatch complete/dismiss (переиспользуется в API)
  TodayViewModels.cs           (~35 строк)  — TodaySummary, TodaySection, TodayCard
```

**Бонус:** `IWorkItemCommandDispatcher` убирает 3 DI-зависимости из TodayModel И убирает дублирование с API.

#### Сложность и риски

- **Сложность: низкая.** Механическое извлечение.
- **Риск: минимальный.** UI не меняется.
- **Время: ~1 час.**

---

## 3. Средние проблемы: разбить при следующем касании

### 3.1 HeuristicStructuredExtractionService — 542 строки, 2 зависимости

**Файл:** `src/SuperChat.Infrastructure/Features/Intelligence/Extraction/HeuristicStructuredExtractionService.cs`

#### В чём проблема

Класс совмещает AI extraction pipeline с парсингом временных выражений (`TryParseTemporalValue` — 77 строк со score-based selection и 5 стратегиями парсинга). Это два разных навыка: оркестрация pipeline и low-level парсинг дат.

#### Что делать

Выделить `TemporalValueParser` — чистый static-класс для парсинга дат/времени:
- `TryParseTemporalValue` (77 строк)
- `NormalizeTemporalValue` (21 строка)
- `HasExplicitOffset` (18 строк)
- `ResolveDueAt` (46 строк)
- `ResolveReferenceTimeZone` (18 строк)

Итого: ~180 строк уходят в отдельный класс, основной сервис становится ~360 строк.

#### Дополнительная проблема

5 пустых catch-блоков для TimeZone exceptions (строки 532-537). Это из списка known issues проекта.

---

### 3.2 SuperChatDbContext — 512 строк, 14 entity-классов в одном файле

**Файл:** `src/SuperChat.Infrastructure/Shared/Persistence/SuperChatDbContext.cs`

#### В чём проблема

14 entity-классов (PilotInviteEntity, AppUserEntity, MagicLinkTokenEntity, ApiSessionEntity, MatrixIdentityEntity, TelegramConnectionEntity, SyncCheckpointEntity, NormalizedMessageEntity, ExtractedItemEntity, WorkItemEntity, MeetingEntity, FeedbackEventEntity, ChunkBuildCheckpointEntity, MeetingProjectionCheckpointEntity, MessageChunkEntity, RetrievalLogEntity) живут в одном файле вместе с DbContext и 260 строками OnModelCreating.

Когда ищешь entity — открываешь файл на 512 строк и скроллишь.

#### Что делать

1. **Вынести entity-классы** по доменным группам:
   - `Persistence/Entities/AuthEntities.cs` — PilotInvite, AppUser, MagicLink, ApiSession
   - `Persistence/Entities/IntegrationEntities.cs` — MatrixIdentity, TelegramConnection
   - `Persistence/Entities/MessagingEntities.cs` — SyncCheckpoint, NormalizedMessage, MessageChunk
   - `Persistence/Entities/IntelligenceEntities.cs` — ExtractedItem, WorkItem, Meeting
   - `Persistence/Entities/OperationEntities.cs` — ChunkBuildCheckpoint, MeetingProjectionCheckpoint, RetrievalLog, FeedbackEvent

2. **Использовать `IEntityTypeConfiguration<T>`** вместо монолитного OnModelCreating (по желанию, не обязательно).

---

### 3.3 ChunkBuilderService — 372 строки

**Файл:** `src/SuperChat.Infrastructure/Features/Intelligence/Retrieval/ChunkBuilderService.cs`

#### В чём проблема

Смешаны два слоя: I/O-логика (загрузка из БД, сохранение checkpoint-ов) и чистый алгоритм разбиения сообщений на чанки (`BuildChunkEntities` — чистая функция без I/O).

#### Что делать

Выделить `ChunkAlgorithm` — static-класс с чистыми функциями:
- `BuildChunkEntities` — группировка по gap/size/count
- `CreateChunkEntity` — создание entity
- `RenderMessageLine` — форматирование строки
- `ComputeContentHash` — SHA256

---

### 3.4 ConversationResolutionService — 346 строк

**Файл:** `src/SuperChat.Infrastructure/Features/Intelligence/Resolution/ConversationResolutionService.cs`

#### В чём проблема

Совмещает загрузку candidates из БД, вызов AI для принятия решений и применение решений к entity-ам. Три фазы в одном классе.

#### Что делать (при следующем касании)

Выделить `ResolutionCandidateLoader` для БД-запросов. Основной сервис остаётся оркестратором.

---

### 3.5 ServiceCollectionExtensions — 308 строк

**Файл:** `src/SuperChat.Infrastructure/Composition/ServiceCollectionExtensions.cs`

#### В чём проблема

48 регистраций сервисов, 16 bindings для Options — всё в одном методе. Пока 308 строк — терпимо. Но каждая новая фича добавляет сюда строки.

#### Что делать (когда перевалит за 400)

Разбить на модульные extension-методы:
- `AddSuperChatOptions(configuration)` — 16 Options bindings
- `AddSuperChatHttpClients(configuration)` — 5 HttpClient
- `AddSuperChatIntelligence()` — Extraction, Retrieval, Chat, Digest
- `AddSuperChatMessaging(configuration, enableWorkers)` — Rebus, workers

---

## 4. Дублирование кода

### 4.1 TimeZone resolve — 2 реализации, одна сломана (КРИТИЧНО)

| Файл | Качество |
|------|----------|
| `Intelligence/Digest/WorkItemTimeZoneResolver.cs` | Правильно — логирует ошибки |
| `Intelligence/Meetings/MeetingTimeSupport.cs` | **Пустые catch-блоки** — ошибки молча проглатываются |

**Что делать:** Создать единый `TimeZoneResolver` в `Infrastructure/Helpers/` и использовать везде.

---

### 4.2 IsSafeLocalReturnUrl — 2 копии (безопасность)

| Файл | Строки |
|------|--------|
| `Web/Program.cs` | 114-120 |
| `Web/Pages/Admin/Unlock.cshtml.cs` | 76-82 |

Идентичная логика проверки URL на безопасность redirect-а. Дублирование в security-коде особенно опасно — если починишь баг в одном месте, забудешь про второе.

**Что делать:** Вынести в `Web/Security/SafeRedirectValidator.cs`.

---

### 4.3 Bearer token parsing — 2 копии (авторизация)

| Файл | Что делает |
|------|-----------|
| `Api/Features/Auth/AuthEndpoints.cs` (96-108) | `TryGetBearerToken` — извлечение токена из заголовка |
| `Api/Features/Auth/ApiSessionAuthenticationHandler.cs` (20-36) | Та же логика inline |

**Что делать:** Вынести в `BearerTokenExtractor.TryExtract()`.

---

### 4.4 Dev-token проверка — 4 файла

Строка `"dev-token-"` как magic string встречается в:
1. `BootstrapMatrixProvisioningService.cs` — создание токена
2. `MatrixProvisioningService.cs` — создание + проверка
3. `TelegramConnectionService.cs` — проверка (уже исправлено → `IsLiveAccessToken`)
4. `MatrixSyncBackgroundService.cs` — проверка в LINQ/SQL

**Что делать:** Создать `DevTokenHelper` с `CreateDevToken(Guid)` и `IsDevToken(string)`.

---

### 4.5 BuildSearchQuery — 2 копии

| Файл |
|------|
| `Web/Pages/WorkItemCardMappings.cs` (45-57) |
| `Web/Pages/ResolvedHistoryCardMappings.cs` (22-34) |

Идентичная логика: взять первый непустой из [title, summary, sourceRoom], обрезать до 80 символов.

**Что делать:** Вынести в `SearchQueryBuilder.Build()`.

---

### 4.6 RecordingHandler в тестах — 6 копий

6 разных вариаций тестового HttpMessageHandler в 6 файлах тестов. Не критично (это тесты), но при 6 копиях стоит унифицировать.

**Что делать (низкий приоритет):** Вынести в `tests/SuperChat.Tests/TestInfrastructure/RecordingHandler.cs`.

---

## 5. DbContext и entity-классы

### Проблема

`SuperChatDbContext.cs` (512 строк) содержит:
- 16 DbSet properties
- 260 строк Fluent API конфигурации в OnModelCreating
- **14 entity-классов прямо в этом же файле**

Это не баг — код работает. Но навигация по entity-ам неудобна: нужно открыть файл на 500+ строк и скроллить.

### Решение

Разнести entity-классы по файлам (см. п. 3.2 выше). DbContext останется ~300 строк — допустимо для composition root persistence.

---

## 6. Порядок работы

### Фаза 1: Быстрые победы (1-2 дня)

| # | Задача | Время | Риск | Эффект |
|---|--------|-------|------|--------|
| 1 | Починить пустые catch в MeetingTimeSupport (TimeZone) | 15 мин | Нулевой | Баги перестанут прятаться |
| 2 | Вынести `IsSafeLocalReturnUrl` в shared helper | 20 мин | Нулевой | Security-код в одном месте |
| 3 | Вынести `BearerTokenExtractor` | 20 мин | Нулевой | Auth-логика в одном месте |
| 4 | Вынести `DevTokenHelper` | 30 мин | Низкий | Magic string в одном месте |
| 5 | Вынести `BuildSearchQuery` | 15 мин | Нулевой | Убрать copy-paste |
| 6 | Вынести TodayViewModels.cs | 15 мин | Нулевой | -35 строк из PageModel |
| 7 | Создать `IWorkItemCommandDispatcher` | 1 час | Низкий | -3 DI из TodayModel, убрать дублирование |

### Фаза 2: Декомпозиция критических классов (3-5 дней)

| # | Задача | Время | Риск |
|---|--------|-------|------|
| 8 | Разбить MatrixSyncBackgroundService | 2-3 часа | Низкий (static методы, механический перенос) |
| 9 | Разбить ChatExperienceService | 2 часа | Низкий (внутренние методы) |
| 10 | Разбить TodayModel | 1 час | Минимальный |

### Фаза 3: Средние проблемы (при следующем касании файла)

| # | Задача | Когда |
|---|--------|-------|
| 11 | Выделить TemporalValueParser из HeuristicStructuredExtractionService | При изменении extraction логики |
| 12 | Разнести entity-классы из SuperChatDbContext | При добавлении новой entity |
| 13 | Выделить ChunkAlgorithm из ChunkBuilderService | При изменении chunking логики |
| 14 | Разбить ServiceCollectionExtensions | Когда перевалит за 400 строк |
| 15 | Унифицировать RecordingHandler в тестах | При рефакторинге тестов |

---

## Метрики успеха

После завершения Фазы 1-2:

| Метрика | До | После |
|---------|-----|-------|
| Максимальный класс (строк) | 706 | ~200 |
| Классы >400 строк с нарушением SRP | 3 | 0 |
| Максимум DI-зависимостей | 10 | 4-5 |
| Дублирований security-кода | 2 | 0 |
| Пустых catch-блоков | 5 | 0 (в scope рефакторинга) |
| Дублирований magic strings | 4 файла | 1 helper |

---

*Документ составлен 30.03.2026 на основе анализа кодовой базы super-chat агентами code-reviewer, type-design-analyzer, test-analyzer, silent-failure-hunter, code-simplifier и Explore.*
