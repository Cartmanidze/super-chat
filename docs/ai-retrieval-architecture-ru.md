# AI Retrieval Architecture: подробный план по этапам

## 1. Зачем нужен этот документ

Сейчас `super-chat` уже умеет:

- получать сообщения из Telegram через `Matrix`
- сохранять их в PostgreSQL
- извлекать из них простые сущности вроде `task`, `meeting`, `commitment`, `waiting`
- показывать результат в компактном chat UI

Но это ещё не полноценная AI retrieval-система.

Пока у нас нет:

- нормального chunking разговора
- векторного поиска по смыслу
- thread summary
- daily summary
- долгой semantic memory
- многошагового retrieval перед ответом LLM

Этот документ нужен, чтобы простым языком зафиксировать:

- какая у нас целевая архитектура
- какие этапы внедрения нужны
- что уже сделано
- что нужно делать дальше
- как двигаться безопасно, не ломая текущий рабочий продукт

## 2. Что уже есть в проекте

На сегодня в проекте уже есть рабочий bootstrap-пайплайн.

Ключевые точки:

- [MatrixSyncBackgroundService.cs](/d:/projects/super-chat/src/SuperChat.Infrastructure/Features/Operations/MatrixSyncBackgroundService.cs)
- [MessageNormalizationService.cs](/d:/projects/super-chat/src/SuperChat.Infrastructure/Features/Messaging/MessageNormalizationService.cs)
- [ExtractionBackgroundService.cs](/d:/projects/super-chat/src/SuperChat.Infrastructure/Features/Operations/ExtractionBackgroundService.cs)
- [ExtractedItemService.cs](/d:/projects/super-chat/src/SuperChat.Infrastructure/Features/Intelligence/Extraction/ExtractedItemService.cs)
- [SearchService.cs](/d:/projects/super-chat/src/SuperChat.Infrastructure/Features/Search/SearchService.cs)
- [ChatExperienceService.cs](/d:/projects/super-chat/src/SuperChat.Infrastructure/Features/Chat/ChatExperienceService.cs)
- [SuperChatDbContext.cs](/d:/projects/super-chat/src/SuperChat.Infrastructure/Shared/Persistence/SuperChatDbContext.cs)

Простыми словами текущий путь такой:

1. Сообщение приходит из Telegram.
2. Через bridge оно доезжает до нашего приложения.
3. Мы сохраняем его в `normalized_messages`.
4. Из него строятся `extracted_items`.
5. UI и API показывают пользователю уже не сырой поток, а более полезную выжимку.

Это хорошая база. Значит, retrieval-слой мы строим не с нуля, а поверх уже работающего ingestion.

## 3. Главный принцип архитектуры

Самая важная идея всей будущей системы:

`Postgres = источник истины`

`Qdrant = быстрый индекс для поиска`

Это означает:

- в PostgreSQL живут настоящие данные
- Qdrant можно пересобрать из PostgreSQL в любой момент
- если Qdrant потерял данные, продукт не должен считаться сломанным навсегда

Именно поэтому в Postgres должны жить:

- сообщения
- чанки
- summaries
- semantic memory
- retrieval logs

А Qdrant должен жить как ускоритель поиска, а не как единственное место хранения знаний.

## 4. Какие системы участвуют в финальной схеме

Ниже вся будущая система простыми словами.

### 4.1. Telegram / другие источники

Это входящие каналы данных.

Сейчас основной источник:

- Telegram через `Matrix + mautrix-telegram`

Позже могут появиться:

- WhatsApp через Matrix bridge
- Signal через Matrix bridge
- email через отдельный direct-ingestion
- VK / MAX и другие сервисы через прямые адаптеры

### 4.2. PostgreSQL

Это основное хранилище.

Здесь должны жить:

- raw/message-level данные
- chunk-level данные
- summaries
- memory
- логи retrieval

### 4.3. Qdrant

Это поиск по смыслу и по словам.

Он нужен, чтобы:

- быстро находить куски переписки по вопросу пользователя
- фильтровать только данные конкретного пользователя
- уметь искать и по смыслу, и по точным словам

### 4.4. Embedding service

Это отдельный сервис, который берёт текст и превращает его в векторы.

На первом практическом этапе он должен уметь:

- dense embeddings
- sparse embeddings

Рекомендуемая модель:

- `BGE-M3`

Почему отдельный сервис:

- экосистема embedding-моделей удобнее в Python
- .NET остаётся orchestration-слоем
- проще обновлять модель независимо от основного приложения

### 4.5. DeepSeek

DeepSeek не должен читать всё подряд.

Он нужен в тех местах, где нужен осмысленный вывод:

- генерация ответа
- summary по теме
- summary по дню
- извлечение memory facts

Очень важный принцип:

в DeepSeek должен идти уже подготовленный, узкий и хороший контекст, а не весь массив сырых сообщений.

## 5. Целевая картина целиком

В финале поток должен выглядеть так:

`Источник сообщений -> Postgres -> Chunk Builder -> Embedding Service -> Qdrant -> Retrieval -> DeepSeek -> Answer + Memory Update`

Если разложить совсем просто:

1. Приходит сообщение.
2. Мы сохраняем его как событие.
3. Мы собираем несколько соседних сообщений в осмысленный chunk.
4. Мы считаем для chunk embedding.
5. Мы кладём chunk в индекс.
6. Пользователь задаёт вопрос.
7. Мы ищем лучший контекст.
8. Только после этого идём в LLM.
9. LLM возвращает структурированный ответ.
10. При необходимости мы обновляем summaries и memory.

## 6. Какие данные должны существовать в системе

Ниже целевая модель данных. Она делится на несколько слоёв.

### 6.1. Сообщения

Это самые близкие к реальности данные.

Сейчас их роль у нас уже выполняет `normalized_messages`.

На данном этапе это нормально. Не нужно немедленно ломать текущую модель и срочно переименовывать таблицу.

Для практических целей сейчас можно считать:

- `normalized_messages` = наши текущие `message_events`

### 6.2. Чанки

Это новые retrieval-единицы.

Таблица:

- `message_chunks`

Смысл:

- не искать по одному сообщению
- искать по маленькой осмысленной сцене разговора

Пример того, что хранится в chunk:

- чей это пользователь
- из какого источника данные
- из какого чата
- с кем разговор
- от какого времени до какого
- текст chunk
- сколько сообщений вошло
- какая версия chunking-логики
- индексирован ли chunk в Qdrant

### 6.3. Thread summaries

Это summary по отдельной теме разговора.

Таблица:

- `thread_summaries`

Пример:

- отдельно summary про договор
- отдельно summary про встречу
- отдельно summary про оплату

### 6.4. Daily summaries

Это summary по дню.

Таблица:

- `daily_summaries`

Очень важно:

daily summary нужно строить не из всех raw messages за день, а из готовых thread summaries.

Это делает систему:

- дешевле по токенам
- стабильнее
- проще для переиндексации

### 6.5. Semantic memory

Это долгоживущие факты.

Таблица:

- `semantic_memory`

Это не “все сообщения”, а устойчивые вещи вроде:

- пользователь обещал отправить договор
- с Иваном идёт разговор про оплату
- Марина ждёт ответ
- у команды встреча по пятницам

### 6.6. Retrieval logs

Это техническая таблица для нас.

Таблица:

- `retrieval_logs`

Она нужна, чтобы понимать:

- какой был вопрос
- какие фильтры применились
- сколько кандидатов нашли
- что ушло в итоговый контекст
- сколько занял retrieval

Без этого retrieval очень трудно дебажить.

## 7. Какой Qdrant нам нужен

### 7.1. Основная коллекция `memory_bgem3_v1`

Это главный retrieval-индекс.

В него кладём:

- `dialog_chunk`
- `thread_summary`
- `daily_summary`
- позже можно добавить `semantic_memory`

Внутри точки должны быть:

- dense vector
- sparse vector
- payload

### 7.2. Payload в основной коллекции

Payload нужен, чтобы фильтровать данные.

Минимально важные поля:

- `user_id`
- `chat_id`
- `peer_id`
- `kind`
- `provider`
- `transport`
- `ts_from`
- `ts_to`

Пример payload:

```json
{
  "user_id": "u_123",
  "provider": "telegram",
  "transport": "matrix_bridge",
  "chat_id": "tg_456",
  "peer_id": "ivan",
  "thread_id": "thread_001",
  "kind": "dialog_chunk",
  "ts_from": 1741734000,
  "ts_to": 1741734300,
  "chunk_id": "ch_001",
  "embedding_version": "bge-m3-v1",
  "chunk_version": 1,
  "language": "ru"
}
```

### 7.3. Почему не “коллекция на каждого пользователя”

Так делать не надо.

Причина простая:

- это дороже
- это сложнее обслуживать
- это хуже масштабируется

Нормальный путь:

- одна коллекция на семейство embeddings
- разделение по пользователям через payload filter

### 7.4. Вторая коллекция `rerank_colbert_v1`

Это уже следующий слой, не этап 1.

Она нужна для:

- ColBERT multivectors
- rerank shortlist-кандидатов

Почему отдельная коллекция:

- BGE-M3 и ColBERT это разные представления текста
- их проще переиндексировать и обновлять независимо
- схема получается чище

## 8. Как должен работать retrieval-алгоритм

Ниже финальная логика простыми шагами.

### Шаг 1. Получаем вопрос пользователя

Например:

- “Что я обещал Ивану?”
- “Какие у меня незакрытые договорённости?”
- “О чём мы говорили сегодня с Мариной?”

### Шаг 2. Сначала фильтруем только данные этого пользователя

Это железное правило.

Нельзя сначала искать по всему индексу, а потом надеяться, что фильтр случайно сработает где-то позже.

Сначала ограничиваем область поиска:

- `user_id == текущий пользователь`

Если из вопроса понятен собеседник:

- добавляем `peer_id`

Если пользователь работает в рамках конкретного чата:

- добавляем `chat_id`

### Шаг 3. Делаем hybrid retrieval

Ищем сразу двумя способами:

- dense retrieval
- sparse retrieval

Dense находит смысл.

Sparse находит точные слова.

Потом объединяем оба списка через `RRF`.

### Шаг 4. Получаем shortlist

Берём примерно:

- `top 40-60`

Это ещё не финальный контекст. Это просто разумный короткий список кандидатов.

### Шаг 5. Если нужно, делаем rerank

Если ColBERT включён:

- пересортировываем shortlist более точной моделью

Если ColBERT не включён:

- остаёмся на hybrid + RRF

### Шаг 6. Расширяем соседний контекст

Для лучших результатов берём:

- сам chunk
- предыдущий chunk
- следующий chunk

Это нужно, потому что один найденный кусок разговора часто слишком узкий.

### Шаг 7. Добавляем summaries и memory

Если есть подходящие summaries:

- thread summary
- daily summary

их тоже можно добавить в контекст.

Если есть подходящая semantic memory:

- добавляем и её

### Шаг 8. Только теперь идём в DeepSeek

В LLM уходит уже не мусорный поток, а аккуратно собранный контекст:

- вопрос
- session tail
- лучшие retrieval chunks
- summaries
- semantic memory

### Шаг 9. Получаем JSON-ответ

DeepSeek должен возвращать структурированный ответ:

- `answer`
- `citations`
- `confidence`
- `memory_updates`

Если нужен summary-режим:

- `summary_json`

Если нужен memory update:

- `memory_update_json`

## 9. Как строить чанки

Один из самых важных моментов всей архитектуры: retrieval не должен искать по одному сообщению.

Проблема одного сообщения:

- оно слишком короткое
- оно часто бессмысленное без соседей
- оно плохо подходит для semantic retrieval

Примеры плохих одиночных сообщений:

- “ок”
- “да”
- “завтра”
- “скину позже”

Поэтому retrieval-единица должна быть не “message”, а `chunk`.

### 9.1. Что такое хороший chunk

Хороший chunk:

- уже содержит локальный смысл
- ещё не слишком длинный
- относится к одной локальной сцене разговора

Простыми словами:

chunk = маленькая сцена разговора

### 9.2. Правила для MVP

Для начала хватит простой логики:

- брать `3-12` соседних сообщений
- не перескакивать через большой time gap
- учитывать reply-chain, если он есть
- ограничивать chunk по длине

### 9.3. Что не нужно делать сразу

Не надо на первом этапе:

- делать сложную ML-кластеризацию тем
- пытаться идеально восстанавливать все thread boundaries
- строить слишком умный semantic segmentation

Для первого рабочего результата нужен не идеальный, а стабильный и понятный chunk builder.

## 10. Как должны работать thread summaries

Thread summary это summary по теме, а не по целому дню.

Например у пользователя в один день могут идти параллельно:

- разговор про договор
- разговор про встречу
- разговор про оплату

Это три разных thread summary.

### 10.1. Когда строить thread summary

Хороший практический критерий:

- когда накопилось `3-5` новых chunk
- или когда тема “замолчала” на `30-60 минут`

### 10.2. Что должно быть внутри

Thread summary должен содержать:

- краткий human-readable summary
- структурированные факты
- unresolved items
- список source chunk ids

### 10.3. Зачем это нужно

Thread summary нужен, чтобы:

- не тащить в retrieval слишком много сырых chunk
- получить более сжатую память по теме
- позже строить daily summary уже не из сырого шума

## 11. Как должны работать daily summaries

Daily summary это summary по всему дню пользователя.

Но строить его нужно не из raw messages, а из thread summaries.

### 11.1. Почему это важно

Такой подход:

- резко уменьшает токены
- делает summaries стабильнее
- уменьшает влияние чатового шума
- упрощает переиндексацию

### 11.2. На какие вопросы отвечает daily summary

Хороший daily summary должен отвечать на вопросы:

- что было важным сегодня
- какие были главные темы
- что осталось незакрытым
- кто ждёт ответа
- какие обязательства появились

### 11.3. Что хранить

Внутри daily summary полезно иметь:

- human-readable текст
- список тем
- список задач
- список встреч
- список commitments
- список waiting_on
- ссылки на thread summaries, из которых всё собрано

## 12. Как должна работать semantic memory

Semantic memory это долгие факты о мире пользователя.

Это не весь чат, а уже нормализованные знания.

Примеры:

- “Иван ждёт коммерческое предложение”
- “По пятницам у команды созвон”
- “Пользователь обещал прислать договор”
- “Марина отвечает за финансы”

### 12.1. Когда её вводить

Не сразу.

Сначала должны стабилизироваться:

- chunking
- retrieval
- summaries

Иначе semantic memory начнёт копить шум.

### 12.2. Зачем она нужна

Она нужна для вопросов не только про “что было недавно”, но и про долгий контекст:

- повторяющиеся обязательства
- устойчивые связи между людьми и темами
- долгие договорённости

## 13. Этапы реализации по порядку

Ниже самый важный раздел документа: пошаговый план внедрения.

## Этап 0. Зафиксировать текущее состояние

### Цель

Понять, что уже есть, и не сломать текущий продукт.

### Что уже есть

- ingestion из Telegram через Matrix
- сохранение в `normalized_messages`
- extraction в `extracted_items`
- chat UI
- базовый поиск

### Что не делаем на этом этапе

- не переписываем всё заново
- не переименовываем половину сущностей только ради красоты

### Результат этапа

У нас есть понятная точка старта.

## Этап 1. База и Qdrant foundation

### Цель

Подготовить фундамент для retrieval, ничего не ломая в текущем поведении приложения.

### Что добавляем

- таблицу `message_chunks`
- таблицу `retrieval_logs`
- `QdrantOptions`
- `IQdrantClient` / `QdrantClient`
- startup bootstrap Qdrant
- сервис `qdrant` в Docker Compose
- базовую коллекцию `memory_bgem3_v1`
- payload indexes

### Что уже сделано

В этом репозитории этап 1 уже заложен локально:

- новые сущности и таблицы в [SuperChatDbContext.cs](/d:/projects/super-chat/src/SuperChat.Infrastructure/Shared/Persistence/SuperChatDbContext.cs)
- schema-upgrade в [PersistenceInitializationHostedService.cs](/d:/projects/super-chat/src/SuperChat.Infrastructure/Features/Operations/PersistenceInitializationHostedService.cs)
- Qdrant config в [QdrantOptions.cs](/d:/projects/super-chat/src/SuperChat.Contracts/Features/Intelligence/Retrieval/QdrantOptions.cs)
- HTTP-клиент в [QdrantClient.cs](/d:/projects/super-chat/src/SuperChat.Infrastructure/Features/Intelligence/Retrieval/QdrantClient.cs)
- bootstrap hosted service в [QdrantInitializationHostedService.cs](/d:/projects/super-chat/src/SuperChat.Infrastructure/Features/Operations/QdrantInitializationHostedService.cs)
- регистрация в [ServiceCollectionExtensions.cs](/d:/projects/super-chat/src/SuperChat.Infrastructure/Composition/ServiceCollectionExtensions.cs)
- инфраструктурные изменения в [infra/docker-compose.yml](/d:/projects/super-chat/infra/docker-compose.yml) и [infra/prod/docker-compose.yml](/d:/projects/super-chat/infra/prod/docker-compose.yml)

### Что важно понимать

Этап 1 не включает:

- реальный chunk builder
- embeddings
- retrieval query path
- summaries
- memory

Это только фундамент.

### Результат этапа

Система готова к следующим шагам и умеет поднимать retrieval-хранилище без включения полноценного retrieval.

## Этап 2. Chunk Builder

### Цель

Начать строить retrieval-единицы из уже существующих сообщений.

### Что делаем

Создаём:

- `IChunkBuilderService`
- `ChunkBuilderService`
- `ChunkBuilderBackgroundService`

Логика:

- читаем `normalized_messages`
- группируем соседние сообщения
- строим `message_chunks`
- помечаем/фиксируем прогресс отдельно от extraction

### Какие правила нужны в MVP

- time gap
- reply chain
- token budget
- ограничение по числу сообщений

### Что не делаем на этом этапе

- не строим сложную semantic segmentation
- не пытаемся идеально восстанавливать весь смысл мира

### Как понять, что этап завершён

- `message_chunks` реально появляются
- они понятны человеку
- один и тот же блок не создаётся заново бесконечно

## Этап 3. Embedding Service

### Цель

Научиться переводить chunk в retrieval-представление.

### Что делаем

Поднимаем отдельный Python-сервис, который:

- принимает текст
- возвращает dense vector
- возвращает sparse vector

Рекомендуемая модель:

- `BGE-M3`

### Почему отдельный сервис

- проще жить с Python ML-экосистемой
- не нагружаем основной .NET-хост ML-зависимостями
- можно обновлять модель отдельно

### Что уже заложено локально

Для этого репозитория этап 3 уже заложен локально как отдельный слой:

- появился `EmbeddingOptions` и .NET-клиент до sidecar-сервиса
- в `infra/embedding-service/` лежит самостоятельный Python HTTP-сервис
- по умолчанию сервис работает в безопасном `mock`-режиме, чтобы stage 3 можно было поднимать без тяжёлой ML-модели
- для реального `BGE-M3` уже есть отдельный provider-path, который включается через `EMBEDDING_PROVIDER=bgem3` и rebuild образа с `EMBEDDING_INSTALL_BGE=1`

Это важно понимать: stage 3 уже не только описан в архитектуре, а материализован как рабочий контракт между .NET и отдельным embedding-сервисом. Следующий практический слой после него — stage 4, который берёт уже построенные чанки и автоматически отправляет их в Qdrant.

### Результат этапа

Мы умеем получать embeddings для chunk и summary.

## Этап 4. Индексация в Qdrant

### Цель

Научиться отправлять chunk в retrieval-индекс.

### Что делаем

После появления chunk:

1. Берём его текст.
2. Получаем embeddings.
3. Делаем upsert point в `memory_bgem3_v1`.
4. Сохраняем `qdrant_point_id` и `indexed_at` в Postgres.

### Что уже заложено локально

Для этого репозитория stage 4 уже заложен как отдельный индексирующий слой:

- появился `ChunkIndexingOptions` с явным poll/batch-контролем
- добавлен `ChunkIndexingService`, который выбирает pending `message_chunks`
- для каждого chunk вызывается `EmbeddingServiceClient`
- после этого выполняется batch upsert в `QdrantClient`
- в Postgres проставляются `embedding_version`, `qdrant_point_id`, `indexed_at`
- фоновый `ChunkIndexingBackgroundService` запускается только там, где включены background workers

Важно: на этом этапе мы сознательно делаем только upsert новых и неотмеченных чанков. Полная зачистка orphaned points после tail-rebuild остаётся отдельной задачей следующего шага, до включения retrieval в пользовательские ответы.

### Что важно

Postgres остаётся источником истины.

Qdrant можно пересобрать из `message_chunks`.

### Результат этапа

В индексе уже есть реальные точки для поиска.

## Этап 5. Retrieval v1

### Цель

Получить первый рабочий retrieval-поиск.

### Что делаем

Создаём:

- `IRetrievalService`
- `RetrievalService`

Он должен:

- принимать `query_text`
- принимать `user_id`
- принимать optional filters
- считать query embeddings
- делать dense + sparse retrieval
- объединять результаты через `RRF`
- возвращать shortlist

### Куда сначала встраиваем

Только в одно место:

- custom/free-text путь в [ChatExperienceService.cs](/d:/projects/super-chat/src/SuperChat.Infrastructure/Features/Chat/ChatExperienceService.cs)

### Почему так

Это самый безопасный rollout:

- если retrieval пустой, можно падать обратно в текущий `SearchService`
- не ломаем весь UI сразу

### Что уже заложено локально

Для этого репозитория stage 5 уже заложен как первый рабочий retrieval-путь:

- появился `RetrievalOptions` и `IRetrievalService`
- `RetrievalService` считает query embeddings через существующий sidecar
- затем он делает hybrid query в `Qdrant` с dense + sparse prefetch и `RRF` fusion
- найденные `chunk_id` подтягиваются обратно из `Postgres`, так что Qdrant по-прежнему не становится источником истины
- каждый retrieval-запрос логируется в `retrieval_logs`
- в `ChatExperienceService` custom-вопрос теперь сначала идёт в retrieval, и только если retrieval ничего не дал или временно сломался, включается fallback на старый `SearchService`

### Результат этапа

Система уже становится retrieval-системой, а не просто search + extraction.

## Этап 6. Реальный DeepSeek JSON flow

### Цель

Перейти от bootstrap seam к реальному AI-ответу.

### Что делаем

Подключаем настоящий клиент DeepSeek для:

- answer prompts
- summary prompts
- extraction prompts

И работаем строго через JSON-ответы.

### Почему это важно

Если LLM отвечает неструктурированно:

- тяжело парсить
- тяжело дебажить
- тяжело гарантировать контракт

### Результат этапа

Custom-вопросы начинают получать не эвристический, а реально собранный AI-ответ.

## Этап 7. Thread Summaries

### Цель

Сжать разговоры по темам и уменьшить нагрузку на retrieval.

### Что делаем

Создаём:

- `IThreadSummaryService`
- `ThreadSummaryService`
- `ThreadSummaryBackgroundService`

Логика:

- накопились новые chunks
- собираем summary по теме
- сохраняем в Postgres
- индексируем в `memory_bgem3_v1`

### Результат этапа

Система умеет помнить не только сырой разговор, но и его сжатый смысл по темам.

## Этап 8. Daily Summaries

### Цель

Собирать память по дню пользователя.

### Что делаем

Создаём:

- `IDailySummaryService`
- `DailySummaryService`
- `DailySummaryBackgroundService`

Логика:

- раз в день берём thread summaries пользователя
- строим daily summary
- сохраняем в Postgres
- индексируем в Qdrant

### Результат этапа

Система начинает отвечать на вопросы уровня:

- “что было важного сегодня”
- “что осталось незакрытым”
- “кто ждёт ответа”

## Этап 9. ColBERT rerank

### Цель

Сделать retrieval точнее на shortlist-кандидатах.

### Что делаем

Добавляем:

- ColBERT service
- коллекцию `rerank_colbert_v1`
- rerank для `top 40-60`

### Когда это делать

Только после того, как уже есть baseline качества на hybrid retrieval.

### Почему не раньше

Иначе можно слишком рано усложнить систему и не понять, что именно реально помогает.

### Результат этапа

Качество top результатов повышается без того, чтобы делать ColBERT first-stage retriever.

## Этап 10. Semantic Memory

### Цель

Добавить долгий слой памяти поверх переписки.

### Что делаем

Создаём:

- `ISemanticMemoryService`
- `SemanticMemoryService`
- таблицу `semantic_memory`
- pipeline обновления memory

### Что будет храниться

- устойчивые факты
- роли людей
- повторяющиеся обязательства
- долгие ожидания и договорённости

### Результат этапа

Система начинает помнить не только “что обсуждали недавно”, но и “что важно знать о мире пользователя”.

## 14. Что пользователь увидит после каждого этапа

### После этапа 1

Пользователь почти ничего нового не увидит.

Это нормальный инфраструктурный этап.

### После этапа 2

Появится внутренняя основа для нормального retrieval.

### После этапа 3-4

Появится настоящий semantic index.

### После этапа 5

Custom-вопросы начнут получать намного более релевантный контекст.

### После этапа 6

Ответы станут более “AI-like”, а не только rule-based.

### После этапа 7-8

Система начнёт лучше работать с длинной перепиской и запросами уровня “что было по теме / за день”.

### После этапа 9-10

Система начнёт лучше справляться со сложными вопросами и долгим контекстом.

## 15. Что нельзя делать сейчас

Ниже список ошибок, которых лучше избегать.

Не надо:

- делать collection per user
- искать по одному сообщению как по основной retrieval-единице
- сразу включать ColBERT как first-stage retriever
- строить daily summary напрямую из всех raw messages дня
- добавлять semantic memory раньше стабильного retrieval baseline
- перегружать смысл существующего `Processed` в `normalized_messages`
- ломать текущий работающий bootstrap ingestion ради “идеальной” будущей схемы

## 16. Практический roadmap именно для этого репозитория

Если говорить прикладно и без лишней теории, то порядок для `super-chat` должен быть таким:

### v1

- считать текущие `normalized_messages` каноническим сырьём
- завершить stage 1 foundation
- сделать `ChunkBuilder`
- поднять embedding service
- индексировать `message_chunks`
- встроить retrieval только в custom chat path

### v2

- добавить `thread_summaries`
- добавить `daily_summaries`
- встроить summaries в retrieval
- подключить реальный DeepSeek JSON flow

### v3

- добавить `semantic_memory`
- добавить `ColBERT`
- перейти к memory-first retrieval pipeline

## 17. Статус на сегодня

На текущий момент:

- bootstrap ingestion уже работает
- extraction уже работает
- chat UI уже работает
- stage 1 foundation уже начат и реализован локально
- stage 2 chunk builder уже реализован локально в безопасном tail-rebuild варианте
- stage 3 embedding service уже реализован локально как отдельный Python sidecar с `mock`-режимом по умолчанию и `BGE-M3`-ready конфигурацией
- retrieval query path ещё не включён
- summaries и semantic memory ещё впереди

То есть проект уже стоит на хорошем фундаменте, но retrieval-архитектура пока только начинает материализоваться.

## 18. Короткий итог

Если совсем коротко, вся идея такая:

- сообщения живут в Postgres
- ищем не по одиночным сообщениям, а по chunk
- Qdrant ускоряет retrieval, но не хранит единственную правду
- summaries сжимают память
- semantic memory добавляет долгий контекст
- DeepSeek должен получать уже подготовленный контекст, а не хаос из сырых чатов

И самое важное:

мы не делаем один гигантский risky rewrite.

Мы строим retrieval-архитектуру поэтапно, рядом с текущим рабочим продуктом.

## 19. Внешние источники, на которые опирается схема

- Qdrant collections and multitenancy: `https://qdrant.tech/documentation/concepts/collections/`
- Qdrant filtering: `https://qdrant.tech/documentation/concepts/filtering/`
- Qdrant hybrid queries and fusion: `https://qdrant.tech/documentation/concepts/hybrid-queries/`
- Qdrant multivectors / late interaction support: `https://qdrant.tech/documentation/concepts/vectors/`
- Qdrant multitenancy guide: `https://qdrant.tech/documentation/guides/multiple-partitions/`
- DeepSeek JSON output: `https://api-docs.deepseek.com/guides/json_mode`
- BGE-M3 model card: `https://huggingface.co/BAAI/bge-m3`
