# Целевая архитектура: почта, VK, MAX и другие источники

## 1. Зачем нужен этот документ

Сейчас `super-chat` уже умеет работать с Telegram через связку:

`Telegram -> mautrix-telegram -> Matrix -> Super Chat`

Это хорошая рабочая схема для одного мессенджера, но если мы хотим идти дальше и подключать:

- `WhatsApp`
- `Signal`
- `Discord`
- `Slack`
- `VK`
- `MAX`
- `Email`
- и другие русские и международные сервисы

то нам нужна более общая архитектура.

Этот документ описывает именно целевую архитектуру:

- как расширять систему, не переписывая всё заново
- что можно переиспользовать из текущего ядра
- какие сущности надо обобщить
- как поддержать одновременно bridge-источники и direct-источники

Документ специально написан простым языком. Это не "теория ради теории", а практический план развития проекта.

## 2. Главная идея

Нам нужно перестать думать так:

- "у нас есть Telegram"

и начать думать так:

- "у нас есть разные источники сообщений"

Эти источники могут приходить к нам разными путями:

1. Через `Matrix bridge`
2. Через `прямой API`
3. Через `IMAP/SMTP`
4. Через `webhook`
5. Через `polling`

То есть важен не только сам сервис, но и способ, которым мы из него читаем данные.

## 3. Какую проблему решает новая архитектура

Сейчас система уже хорошо умеет:

- принимать поток сообщений
- нормализовать его
- извлекать смысл
- показывать результат в `Today`, `Waiting`, `Search`

Но входной слой всё ещё слишком Telegram-first.

Сейчас в коде есть явные признаки этого:

- отдельная сущность `TelegramConnection`
- отдельная таблица `telegram_connections`
- отдельный API route `/integrations/telegram`
- sync-воркер знает именно про `TelegramConnection`
- в `normalized_messages` сейчас пишется `source = "telegram"`

Это нормально для пилота, но это мешает росту в сторону:

- `WhatsApp`
- `Signal`
- `VK`
- `MAX`
- `Email`

## 4. Принцип, на котором должна строиться новая система

Новая система должна делиться на два уровня:

### Уровень 1. Источник

Источник отвечает на вопрос:

- откуда пришли сообщения

Примеры источников:

- `Telegram`
- `WhatsApp`
- `Signal`
- `Discord`
- `Slack`
- `VK`
- `MAX`
- `Email`

### Уровень 2. Транспорт

Транспорт отвечает на вопрос:

- как технически мы получаем сообщения из источника

Примеры транспорта:

- `MatrixBridge`
- `DirectApi`
- `ImapSmtp`
- `Webhook`

Один и тот же источник может использовать разные транспорты.

Например:

- `Telegram` сейчас у нас идёт через `MatrixBridge`
- `WhatsApp` логично тоже вести через `MatrixBridge`
- `Email` логично вести через `ImapSmtp` или provider API
- `VK` и `MAX` скорее всего пойдут через `DirectApi` или `Webhook`, а не через Matrix

## 5. Ключевой вывод

Нам не нужна архитектура "под Telegram и ещё пару интеграций".

Нам нужна архитектура "под любые источники сообщений".

При этом:

- bridge-источники должны жить рядом
- direct-источники должны жить рядом
- всё после нормализации должно быть общим

То есть правильная цель такая:

`Источник -> адаптер источника -> нормализованное сообщение -> extraction -> продуктовые экраны`

## 6. Какие типы интеграций мы хотим поддержать

### 6.1. Bridge-first источники

Это сервисы, которые удобно подключать через Matrix bridge.

Сюда хорошо подходят:

- `Telegram`
- `WhatsApp`
- `Signal`
- `Discord`
- `Slack`
- часть других чат-платформ, где уже есть mature bridge

Почему это удобно:

- bridge уже решает большую часть боли
- он сам связывает внешний сервис и Matrix
- наше приложение может читать единый поток Matrix-событий

### 6.2. Direct-first источники

Это сервисы, которые лучше подключать напрямую.

Сюда хорошо подходят:

- `Email`
- `VK`
- `MAX`
- российские внутренние мессенджеры без зрелого Matrix bridge
- любые сервисы, где есть хороший REST API, webhook API или IMAP/API слой

Почему это лучше:

- не нужен лишний промежуточный слой
- меньше точек отказа
- мы не теряем важную модель данных
- можно точнее контролировать авторизацию, курсоры и метаданные

## 7. Что должно остаться общим для всех источников

Очень важно не переписать заново весь продукт под каждый источник.

Общими должны остаться:

- пользователь продукта
- экран подключения интеграций
- хранилище нормализованных сообщений
- extraction
- `Today`
- `Waiting`
- `Search`
- feedback
- health и мониторинг

Именно это и есть ценность продукта. Источник должен менять только слой ingestion и connection flow.

## 8. Новая модель системы

Ниже целевая схема:

`Provider -> Transport Adapter -> Sync Orchestrator -> Normalization -> Extraction -> Digest/Search/UI`

Расшифровка:

1. `Provider`
   - Telegram, WhatsApp, Email, VK, MAX и так далее
2. `Transport Adapter`
   - знает, как именно подключаться и читать события
3. `Sync Orchestrator`
   - запускает sync по всем активным подключениям
4. `Normalization`
   - приводит сообщения к единому виду
5. `Extraction`
   - извлекает задачи, встречи, обещания, ожидания
6. `Digest/Search/UI`
   - показывает это пользователю

## 9. Какие новые сущности нужны

Ниже список сущностей, которые стоит ввести.

### 9.1. `IntegrationProvider`

Это перечисление источников.

Примеры значений:

- `Telegram`
- `WhatsApp`
- `Signal`
- `Discord`
- `Slack`
- `VK`
- `Max`
- `Email`

Важно:

это именно "что за сервис", а не "как мы к нему подключаемся".

### 9.2. `IntegrationTransport`

Это перечисление способов доставки данных.

Примеры значений:

- `MatrixBridge`
- `DirectApi`
- `ImapSmtp`
- `Webhook`

### 9.3. `IntegrationConnection`

Это новое общее состояние подключения.

Оно должно заменить чисто Telegram-специфичную модель.

Пример того, что там должно быть:

- `Id`
- `UserId`
- `Provider`
- `Transport`
- `State`
- `ExternalAccountId`
- `DisplayName`
- `LastSyncedAt`
- `LastError`
- `CreatedAt`
- `UpdatedAt`

### 9.4. `TransportIdentity`

Это техническая личность, которая нужна конкретному транспорту.

Примеры:

- Matrix user + access token
- OAuth access token для Gmail
- OAuth refresh token для Microsoft
- VK service token
- MAX access token

Это важно отделить от обычного `AppUser`, потому что:

- у одного пользователя может быть несколько интеграций
- у интеграции может быть своя техника логина
- токены и cursor-состояние не должны быть зашиты в одну старую сущность

### 9.5. `Conversation`

Нам нужна единая модель разговора.

Это может быть:

- личный чат
- группа
- канал
- email thread
- диалог
- служебная лента

Что там должно быть:

- `ConnectionId`
- `ExternalConversationId`
- `Kind`
- `Title`
- `ParticipantCount`
- `IsDirect`
- `IsBroadcast`
- `MetadataJson`

### 9.6. `SyncCursor`

У каждого транспорта свои курсоры.

Примеры:

- Matrix `next_batch`
- IMAP `UIDVALIDITY + last UID`
- Gmail history id
- VK long poll cursor
- webhook checkpoint

Нам нужна единая таблица курсоров, а не транспортная логика, раскиданная по разным местам.

### 9.7. `NormalizedMessage`

Это уже почти есть, и это хорошая сущность.

Но её нужно сделать менее Telegram/Matrix-специфичной.

Что должно быть в общей форме:

- `UserId`
- `ConnectionId`
- `Provider`
- `Transport`
- `ConversationId`
- `ExternalMessageId`
- `ExternalThreadId`
- `SenderId`
- `SenderDisplayName`
- `Text`
- `SentAt`
- `MetadataJson`
- `Processed`

А transport-specific идентификаторы лучше хранить:

- либо в `MetadataJson`
- либо в отдельных side tables

Пример:

- для Matrix: `matrix_room_id`, `matrix_event_id`
- для email: `internet_message_id`, `thread_id`, `folder`
- для VK: `peer_id`, `conversation_message_id`

## 10. Как должен выглядеть новый слой абстракций

### 10.1. Главный принцип

Сейчас у нас есть инфраструктурные сервисы, которые знают слишком много про конкретный источник.

Нужно перейти к контрактам вида:

- "подключи источник"
- "прочитай новые события"
- "узнай метаданные разговора"
- "нормализуй сообщение"

### 10.2. Предлагаемые интерфейсы

Ниже не код, а концептуальная схема.

#### `IIntegrationConnectionService`

Отвечает за:

- старт подключения
- статус подключения
- отключение
- обновление состояния

Это должен быть общий фасад вместо Telegram-only surface.

#### `IMessageSourceAdapter`

Это главный адаптер источника.

Он должен уметь:

- определить, поддерживает ли он конкретный `Provider + Transport`
- начать connection flow
- читать новые события
- вернуть курсор
- вернуть метаданные разговора

### `ISourceSyncAdapter`

Можно сделать и более узкий вариант:

- только sync
- только курсоры
- только список новых сообщений

### `IConversationMetadataAdapter`

Отвечает за:

- название разговора
- тип разговора
- размер разговора
- флаг `channel / broadcast`
- direct/group/thread detection

### `IConnectionBootstrapper`

Нужен для источников, где есть отдельный flow авторизации.

Примеры:

- Matrix bridge login
- OAuth for Gmail
- OAuth for Microsoft
- OAuth/API token for VK
- OAuth/API token for MAX

## 11. Какие адаптеры нам нужны по факту

### 11.1. `MatrixBridgeSourceAdapter`

Это первый общий адаптер семейства bridge-интеграций.

Он должен покрывать:

- `Telegram`
- `WhatsApp`
- `Signal`
- `Discord`
- `Slack`

Его зона ответственности:

- скрытая Matrix identity
- bridge login orchestration
- `/sync`
- auto-join invited rooms
- room metadata
- room display names

Важно:

этот адаптер должен стать не "Telegram adapter", а именно "Matrix bridge adapter".

Telegram внутри него будет просто одним из провайдеров.

### 11.2. `EmailSourceAdapter`

Это отдельный адаптер для почты.

Он не должен идти через Matrix.

Он должен уметь:

- подключать ящик
- читать новые письма
- разбирать треды
- доставать тему, отправителя, адресатов, тело, вложения
- резать quoted history при необходимости

Он может работать через:

- `IMAP` для чтения
- `SMTP` для отправки
- provider API, если это выгоднее

### 11.3. `VkSourceAdapter`

Это direct API / webhook адаптер.

Он должен уметь:

- читать личные сообщения
- читать групповые диалоги
- определять тип диалога
- фильтровать каналы и публичные ленты, если они нам не нужны

### 11.4. `MaxSourceAdapter`

Если `MAX` даёт API, webhook или long-poll transport, то он должен жить как обычный direct adapter.

То есть:

- не подстраивать архитектуру под `MAX`
- а встроить `MAX` в уже готовый контракт `IMessageSourceAdapter`

Именно это и есть цель новой архитектуры.

## 12. Что делать с почтой

Почта требует отдельной логики, потому что письмо — это не чат-сообщение один в один.

Нужно явно учитывать:

- `subject`
- `from`
- `to`
- `cc`
- `bcc`, если доступно
- `message-id`
- `in-reply-to`
- `references`
- `thread`
- `attachments`
- `folder / label`

### 12.1. Как поддержать Gmail, Outlook, Яндекс и другие

Надо разделить:

- транспорт чтения
- транспорт авторизации

Пример:

- чтение может быть через `IMAP`
- авторизация может быть через:
  - `OAuth`
  - `App Password`
  - логин/пароль, если provider это допускает

### 12.2. Нужен отдельный слой auth-strategy

Например:

- `IProviderAuthStrategy`

Реализации:

- `GoogleOAuthStrategy`
- `MicrosoftOAuthStrategy`
- `YandexOAuthStrategy`
- `YandexAppPasswordStrategy`
- `YahooAppPasswordStrategy`
- `MailRuOAuthStrategy`

Тогда сам `EmailSourceAdapter` останется общим, а auth будет подменяться по провайдеру.

## 13. Что делать с VK, MAX и другими русскими сервисами

Здесь правильный принцип такой:

- не ждать, пока для каждого сервиса появится Matrix bridge
- а считать эти сервисы direct-first источниками

То есть архитектура должна позволять:

- одному источнику жить через `MatrixBridge`
- другому через `DirectApi`
- третьему через `Webhook`

без того, чтобы всё приложение знало детали каждого сервиса.

### 13.1. Что нужно уметь для русских сервисов

Нам важны такие capability flags:

- `SupportsDirectChats`
- `SupportsGroupChats`
- `SupportsChannels`
- `SupportsThreads`
- `SupportsAttachments`
- `SupportsReadReceipts`
- `SupportsHistoryBackfill`
- `SupportsWebhooks`
- `SupportsIncrementalSync`

Тогда продукт не будет предполагать, что все сервисы одинаковые.

## 14. Как должен выглядеть sync после рефактора

Сейчас sync завязан на конкретный Telegram/Matrix pipeline.

Нужно перейти к общему orchestrator.

### Целевая схема

`SourceSyncHostedService`

Он должен:

1. выбрать активные `IntegrationConnection`
2. сгруппировать их по `Provider + Transport`
3. для каждой группы взять подходящий адаптер
4. выполнить sync
5. записать новые `NormalizedMessage`
6. обновить `SyncCursor`
7. обновить `LastSyncedAt`
8. обновить состояние подключения

То есть host service должен оркестрировать, а не знать детали Telegram, Gmail или VK.

## 15. Какие правила фильтрации должны стать общими

Сейчас у нас уже есть полезные правила:

- management room не ingestится
- большие группы не ingestятся
- каналы не ingestятся
- `Today` строится только за текущий день

Эти правила нужно сделать провайдер-независимыми.

### Примеры

- для Telegram канал = `broadcast`
- для email thread каналов вообще нет
- для VK может быть паблик, беседа, личка
- для Discord может быть guild channel, DM, thread

Поэтому фильтр должен работать не по сырому типу сервиса, а по общей модели:

- `ConversationKind`
- `IsDirect`
- `IsBroadcast`
- `ParticipantCount`

## 16. Что уже можно переиспользовать без большого переписывания

Из текущей системы хорошо переиспользуются:

- `normalized_messages` как идея
- extraction pipeline
- `ExtractedItemKind`
- `Today`
- `Waiting`
- `Search`
- health snapshot
- feedback
- локализация UI

То есть продуктовая ценность уже построена. Нам нужно обобщить ingress, а не переписать всё.

## 17. Что придётся изменить в текущем коде

### 17.1. В `Contracts`

Нужно добавить:

- `IntegrationProvider`
- `IntegrationTransport`
- `ConnectionState`
- provider/transport capability модели
- новые options для direct adapters

### 17.2. В `Domain`

Нужно добавить:

- общую модель `IntegrationConnection`
- общую модель `Conversation`
- правила фильтрации по `ConversationKind`

`Digest` и extraction можно оставить почти как есть.

### 17.3. В `Infrastructure`

Нужно добавить:

- общий adapter registry
- `MatrixBridgeSourceAdapter`
- `EmailSourceAdapter`
- `VkSourceAdapter`
- `MaxSourceAdapter`
- общую оркестрацию sync
- общую таблицу курсоров

И постепенно убрать жёсткую зависимость кода от Telegram-only сущностей.

### 17.4. В `Web` и `Api`

Нужно перейти от одного экрана:

- `Connect Telegram`

к общему разделу:

- `Integrations`

Внутри него уже можно иметь карточки:

- Telegram
- WhatsApp
- Signal
- Email
- VK
- MAX

## 18. Как должна выглядеть новая база данных

Ниже не точная миграция, а целевая модель.

### Таблица `integration_connections`

Хранит:

- кто подключил
- какой источник
- какой транспорт
- в каком состоянии подключение
- когда последний раз синкалось
- какая была последняя ошибка

### Таблица `transport_identities`

Хранит:

- токены
- service user ids
- refresh tokens
- transport-specific account data

### Таблица `sync_cursors`

Хранит:

- курсор
- scope курсора
- transport/provider
- время обновления

### Таблица `conversations`

Хранит:

- внешний id разговора
- заголовок
- тип
- число участников
- флаг direct
- флаг broadcast

### Таблица `normalized_messages`

Остаётся ключевой, но становится более общей:

- provider
- transport
- connection id
- conversation id
- external message id
- sender
- text
- sent at
- metadata

### Таблица `extracted_items`

Остаётся почти в том же виде.

Это хорошо: extraction слой у нас уже достаточно общий.

## 19. Как мигрировать без большого взрыва

Не надо пытаться сделать всё сразу.

Нужно идти по этапам.

### Этап 1. Обобщить модель подключения

Сначала:

- ввести `IntegrationProvider`
- ввести `IntegrationTransport`
- ввести `integration_connections`
- оставить текущий Telegram flow работать через новый фасад

На этом этапе можно даже временно хранить старые таблицы рядом.

### Этап 2. Вынести Matrix-bridge семью в общий адаптер

Потом:

- выделить `MatrixBridgeSourceAdapter`
- внутри него оставить поддержку Telegram
- потом добавить WhatsApp/Signal

### Этап 3. Ввести direct-source слой

Потом:

- добавить `EmailSourceAdapter`
- добавить базовые auth strategies
- добавить поддержку IMAP/OAuth/app password

### Этап 4. Добавить VK/MAX

Потом:

- сделать `VkSourceAdapter`
- сделать `MaxSourceAdapter`
- встроить их в общий `Integrations` UI

### Этап 5. Переехать на единый `Integrations` UX

Когда backend уже обобщён:

- убрать Telegram-first wording из UI и API
- сделать общий каталог интеграций

## 20. Какая целевая последовательность внедрения самая разумная

Я бы рекомендовал такой порядок:

1. Архитектурно обобщить `Telegram` в `bridge-based integration`
2. Добавить `WhatsApp`
3. Добавить `Signal`
4. После этого строить `Email` как отдельное направление
5. Потом подключать `VK`
6. Потом подключать `MAX`

Почему так:

- WhatsApp/Signal ближе к текущему Telegram ядру
- Email требует другой модели данных и auth-flow
- VK/MAX лучше делать уже поверх готового generic direct-source каркаса

## 21. Что важно не сломать при этом

При рефакторе нужно сохранить:

- текущий рабочий Telegram flow
- текущие правила фильтрации
- текущую extraction pipeline
- текущие health endpoints
- текущий deploy flow

То есть рефактор должен расширять систему, а не ломать то, что уже работает.

## 22. Архитектурные правила, которые стоит зафиксировать

### Правило 1

UI и API не должны знать детали конкретного источника.

Они должны говорить с общим integration service.

### Правило 2

Hosted services не должны быть Telegram-only.

Они должны оркестрировать адаптеры.

### Правило 3

`normalized_messages` должны быть provider-agnostic.

### Правило 4

Transport-specific id и metadata не должны определять всю модель продукта.

### Правило 5

Фильтрация должна идти по общей модели разговора:

- direct
- group
- broadcast
- participant count
- thread

### Правило 6

Почта должна жить как отдельный transport family, а не как искусственный Matrix-чат.

### Правило 7

VK, MAX и другие русские сервисы должны подключаться через generic direct adapter contract, а не через точечные хардкоды.

## 23. Практический итог

Если сказать совсем просто, целевая архитектура должна быть такой:

- `Telegram / WhatsApp / Signal / Discord / Slack` -> чаще через `MatrixBridgeSourceAdapter`
- `Email` -> через `EmailSourceAdapter`
- `VK / MAX / другие русские сервисы` -> через `DirectApiSourceAdapter`

А дальше для всех одинаково:

- `Normalization`
- `Extraction`
- `Today`
- `Waiting`
- `Search`

Это и есть правильное направление развития проекта.

## 24. Что делать следующим шагом

Следующий инженерный шаг я бы выбрал такой:

1. Ввести общие enum и модели:
   - `IntegrationProvider`
   - `IntegrationTransport`
   - `IntegrationConnection`
2. Сделать новый общий API/Service слой для интеграций.
3. Оставить Telegram работать через него как первую реализацию.
4. Только после этого добавлять второй источник.

Именно так система будет расти спокойно, а не превратится в набор специальных кейсов под каждый сервис.
