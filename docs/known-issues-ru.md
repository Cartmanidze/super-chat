# Super Chat: известные проблемы и технический долг

Дата анализа: 2026-03-22

---

## КРИТИЧЕСКИЕ (требуют немедленного внимания)

### 1. Production секреты зачекинены в репозиторий

**Файл:** `infra/prod/.env`

**Проблема:** файл содержит реальный `SUPERCHAT_ADMIN_PASSWORD_HASH`, `MAUTRIX_TELEGRAM_API_ID`, `MAUTRIX_TELEGRAM_API_HASH`, email'ы пользователей. Этот файл находится под version control.

**Риск:** полная компрометация production при утечке репозитория.

**Рекомендация:**
- Добавить `infra/prod/.env` в `.gitignore`
- Использовать secrets management (Docker secrets, HashiCorp Vault, или хотя бы environment variables на сервере)
- Ротировать все текущие секреты после фикса
- Оставить только `infra/prod/.env.example` с placeholder'ами

**Приоритет:** P0

---

### 2. Telegram API credentials всё ещё невалидны (блокер пилота)

**Файл:** `docs/pilot-next-steps.md`

**Проблема:** Telegram login через bridge падает с `ApiIdInvalidError`. Нужен валидный `api_id` / `api_hash` с `my.telegram.org`.

**Статус:** описано как текущий блокер в документации. Без решения этой проблемы Telegram-интеграция не работает в production.

**Приоритет:** P0

---

## ВЫСОКИЙ ПРИОРИТЕТ

### 3. `DevSeedSampleData = true` по умолчанию

**Файл:** `src/SuperChat.Contracts/Features/Auth/PilotOptions.cs:13`

**Проблема:** дефолтное значение `DevSeedSampleData` — `true`. Это значит, что если при деплое забудут явно выставить `false`, production будет работать в dev-режиме с demo-данными вместо реальной синхронизации.

**Рекомендация:** изменить дефолт на `false`, включать явно только для development.

**Приоритет:** P1

---

### 4. Hardcoded PostgreSQL credentials в appsettings.json

**Файлы:**
- `src/SuperChat.Api/appsettings.json:13`
- `src/SuperChat.Web/appsettings.json:13`

**Проблема:** строка подключения с `Username=postgres;Password=postgres` захардкожена в appsettings.json. Хотя в production используются переменные окружения, эти файлы под version control и могут быть случайно использованы.

**Рекомендация:** убрать credentials из appsettings.json, оставить только в `.env.example`.

**Приоритет:** P1

---

### 5. Пустые catch-блоки для TimeZone исключений (5 мест)

**Файлы:**
- `src/SuperChat.Web/Pages/Admin/Index.cshtml.cs:103-108`
- `src/SuperChat.Infrastructure/Features/Intelligence/Resolution/DeepSeekResolutionService.cs:145-150`
- `src/SuperChat.Infrastructure/Features/Intelligence/Extraction/DeepSeekStructuredExtractionService.cs:241-246`
- `src/SuperChat.Infrastructure/Features/Intelligence/Extraction/HeuristicStructuredExtractionService.cs:532-537`
- `src/SuperChat.Infrastructure/Features/Intelligence/Meetings/MeetingTimeSupport.cs:20-25`

**Проблема:** `TimeZoneNotFoundException` и `InvalidTimeZoneException` ловятся и молча проглатываются. Если `TodayTimeZoneId` настроен неправильно, система тихо падает на UTC без логирования.

**Рекомендация:** добавить `logger.LogWarning(...)` в каждый catch-блок.

**Приоритет:** P1

---

### 6. Широкий catch(Exception) в DeepSeekResolutionService

**Файл:** `src/SuperChat.Infrastructure/Features/Intelligence/Resolution/DeepSeekResolutionService.cs:48-52`

**Проблема:** `catch (Exception)` ловит все исключения (сеть, таймауты, NullReference, любые баги) и возвращает пустой массив. Это маскирует реальные ошибки.

**Рекомендация:** ловить только ожидаемые исключения (`HttpRequestException`, `OperationCanceledException`, `JsonException`).

**Приоритет:** P1

---

### 7. Много мест с catch(Exception) — потенциальное маскирование багов

**Файлы (30 мест в production коде):**
- `MatrixSyncBackgroundService.cs` (6 мест)
- `PipelineCommandHandlers.cs` (4 места)
- `TelegramConnectionService.cs` (2 места)
- `ChatExperienceService.cs` (2 места)
- `ChatAnswerGenerationService.cs` (1)
- `DeepSeekJsonClient.cs` (1)
- `EmbeddingServiceClient.cs` (1)
- `QdrantClient.cs` (2)
- `RetrievalService.cs` (2)
- `TextEnrichmentClient.cs` (1)
- `MatrixRoomDisplayNameService.cs` (1)
- `ConversationResolutionCommandHandlers.cs` (2)
- `DeepSeekStructuredExtractionService.cs` (1)
- `ChunkIndexingService.cs` (1)
- `QdrantBootstrapRunner.cs` (2)

**Проблема:** большинство из них логируют ошибку и продолжают работу (что правильно для background services), но некоторые могут маскировать серьёзные баги. Стоит пересмотреть каждый случай.

**Рекомендация:** аудит каждого catch-блока. Background services — OK для resilience. HTTP clients — предпочтительнее ловить конкретные типы.

**Приоритет:** P2

---

## СРЕДНИЙ ПРИОРИТЕТ

### 8. Нереализованные провайдеры интеграций

**Файл:** `src/SuperChat.Api/Features/Integrations/IntegrationEndpoints.cs:54-57, 85-88, 111-114`

**Проблема:** эндпоинты `GET/POST/DELETE /api/v1/integrations/{provider}` существуют для любого провайдера, но для всех кроме Telegram бросают `NotSupportedException` → 501 Not Implemented. Клиенты могут попытаться подключить WhatsApp/Signal и получить ошибку.

**Рекомендация:** добавить validation и документировать, какие провайдеры поддерживаются. Или убрать generic эндпоинты до реализации новых провайдеров.

**Приоритет:** P2

---

### 9. Молчаливая ошибка при невалидном AdminPasswordHash

**Файл:** `src/SuperChat.Web/Security/AdminPasswordService.cs:48-50`

**Проблема:** если base64-декодирование хеша пароля админа падает с `FormatException`, возвращается пустая строка без логирования. Админ-пароль становится невалидным без какой-либо индикации.

**Рекомендация:** логировать ошибку декодирования.

**Приоритет:** P2

---

### 10. Eвристическая extraction — fallback создаёт шум

**Файл:** `src/SuperChat.Infrastructure/Features/Intelligence/Extraction/HeuristicStructuredExtractionService.cs`

**Проблема:** если эвристика не может определить тип сообщения, создаётся fallback-карточка `Task` с заголовком `Follow-up candidate`. Это приводит к шумным карточкам, которые снижают доверие пользователя к системе.

**Рекомендация:** повысить порог уверенности для создания fallback-карточек или пропускать неуверенные результаты.

**Приоритет:** P2

---

### 11. Пустые catch-блоки в тестах

**Файлы:**
- `tests/SuperChat.Api.Tests/ApiSmokeTests.cs:593-595`
- `tests/SuperChat.Web.Tests/SmokeTests.cs:179-181`

**Проблема:** пустые `catch` без типа исключения. В тестах это менее критично, но может скрывать падающие сценарии.

**Рекомендация:** как минимум логировать или добавить комментарий, почему исключение намеренно игнорируется.

**Приоритет:** P3

---

### 12. Отсутствие тестов для некоторых критичных путей

**Наблюдение:**
- 51 файл тестов, хорошее покрытие основных сценариев
- Но нет выделенных тестов для:
  - AdminPasswordService (хеширование, верификация)
  - Admin unlock flow
  - Обработка невалидного `TodayTimeZoneId`
  - Matrix provisioning edge cases (network failures)
  - TelegramConnectionService reconnect flow

**Рекомендация:** добавить тесты для security-critical путей.

**Приоритет:** P2

---

### 13. Singleton-регистрации для сервисов с DbContext

**Файл:** `src/SuperChat.Infrastructure/Composition/ServiceCollectionExtensions.cs:161-208`

**Проблема:** большинство сервисов зарегистрированы как `Singleton`, но используют `IDbContextFactory<SuperChatDbContext>` для создания DbContext. Это корректно архитектурно (factory pattern), но нетипично для EF Core приложений и может путать новых разработчиков. Важно помнить, что каждый метод сервиса должен создавать свой scope через factory.

**Рекомендация:** добавить комментарий в `ServiceCollectionExtensions.cs`, объясняющий, почему Singleton + DbContextFactory вместо Scoped.

**Приоритет:** P3

---

## НИЗКИЙ ПРИОРИТЕТ / УЛУЧШЕНИЯ

### 14. Hardcoded localhost URL'ы в Options-классах

**Файлы:**
- `src/SuperChat.Contracts/Features/Auth/PilotOptions.cs` — `https://localhost:8080`
- `src/SuperChat.Contracts/Features/Intelligence/Retrieval/QdrantOptions.cs` — `http://localhost:6333`
- `src/SuperChat.Contracts/Features/Intelligence/Retrieval/EmbeddingOptions.cs` — `http://localhost:7291`
- `src/SuperChat.Contracts/Features/Intelligence/Extraction/TextEnrichmentOptions.cs` — `http://localhost:7391`

**Проблема:** дефолтные значения URL'ов hardcoded в options-классах. Это нормально для development, но создаёт зависимость от конкретных портов.

**Приоритет:** P3

---

### 15. Файл `now()` в корне проекта

**Файл:** `now()` (пустой файл в корне)

**Проблема:** случайно созданный файл, вероятно результат ошибочной команды.

**Рекомендация:** удалить.

**Приоритет:** P3

---

### 16. Документация pilot-next-steps содержит устаревшие данные

**Файл:** `docs/pilot-next-steps.md`

**Проблема:** документ описывает конкретный блокер и follow-up задачи. Если блокер уже решён, документ нуждается в обновлении. Если нет — он должен быть в README или issue tracker.

**Приоритет:** P3

---

## ПОЗИТИВНЫЕ НАБЛЮДЕНИЯ

Для объективности стоит отметить, что сделано хорошо:

1. **Чистая архитектура** — чёткое разделение слоёв, зависимости идут в одном направлении
2. **Хороший набор тестов** — 51 файл тестов, покрытие ключевых сценариев
3. **Правильный auth** — PBKDF2-SHA256 с 100k итераций, FixedTimeEquals для сравнения хешей
4. **Feature-based structure** — код организован по фичам, а не по техническим слоям
5. **Полная инфраструктура** — Docker Compose для dev и prod, CI/CD, Grafana дашборды
6. **Грамотная обработка ошибок в background services** — resilient pattern, логирование, продолжение работы
7. **Хорошая документация** — 7+ документов, включая подробный guide на русском
8. **Расширяемая архитектура** — подготовка под multichannel (WhatsApp, Signal) через `IntegrationProvider` enum
9. **Правильная фильтрация** — использование Telegram-side metadata вместо Matrix membership
10. **Полиморфные work items** — гибкая система с Request/Event/ActionItem и стратегиями
