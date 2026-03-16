# SuperChat API: подробная документация

## 1. Что это за API

`SuperChat.Api` — это JSON API проекта `super-chat`.

Полный префикс всех endpoint'ов:

```text
/api/v1
```

Текущий production base URL:

```text
https://api.tranify.ru/api/v1
```

Локально обычно это выглядит так:

```text
https://localhost:<port>/api/v1
```

API сейчас используется для:

- проверки здоровья системы
- auth по magic link и выдачи bearer token
- получения профиля текущего пользователя
- управления интеграциями, прежде всего Telegram
- chat-запросов к продуктовой логике
- получения dashboard-выжимок
- поиска
- записи feedback

## 2. Общие правила API

### 2.1. Формат данных

API работает с JSON.

Для запросов с телом используй:

```http
Content-Type: application/json
```

Ответы тоже приходят в JSON.

### 2.2. Версионирование

Текущая версия API зафиксирована в URL:

```text
/api/v1
```

### 2.3. Аутентификация

Почти все endpoint'ы, кроме health и auth-входа, требуют bearer token:

```http
Authorization: Bearer <access_token>
```

Схема аутентификации внутри проекта называется `ApiSession`.

### 2.4. Сессии

Bearer token — это не JWT, а серверная API session, которая хранится в базе.

Сейчас логика такая:

- после magic link пользователь получает session token
- token хранится в таблице API sessions
- при каждом запросе сервер ищет его в БД
- если срок жизни истёк, токен удаляется и запрос считается неавторизованным

Текущий default lifetime сессии:

```text
30 дней
```

Это берётся из `SuperChat:ApiSessionDays`.

### 2.5. Время и даты

Во всех API-моделях даты уходят как `DateTimeOffset`, то есть в ISO 8601 формате.

Пример:

```json
"2026-03-14T10:32:15+00:00"
```

Важно:

- day-boundary для `Today` в продуктовой логике сейчас считается по `Europe/Moscow`
- но сами поля API возвращаются как обычные ISO timestamps

### 2.6. Типовые статус-коды

- `200 OK` — успешный запрос
- `202 Accepted` — запрос принят, но это скорее асинхронный шаг
- `204 No Content` — успешный запрос без тела ответа
- `400 Bad Request` — неверный token или ошибка валидации
- `401 Unauthorized` — нет bearer token или он невалиден
- `403 Forbidden` — доступ запрещён, например magic link request отклонён
- `404 Not Found` — неверный route segment, например неизвестный provider
- `501 Not Implemented` — provider зарезервирован в маршрутах, но ещё не реализован

## 3. Быстрый auth flow

Обычный путь работы клиента сейчас такой:

1. вызвать `POST /api/v1/auth/magic-links`
2. получить `developmentLink`
3. достать из него одноразовый `token`
4. вызвать `POST /api/v1/auth/token-exchange`
5. получить `accessToken`
6. использовать его в `Authorization: Bearer ...`

Пример:

```bash
curl -X POST "https://api.tranify.ru/api/v1/auth/magic-links" \
  -H "Content-Type: application/json" \
  -d '{"email":"pilot@example.com"}'
```

Потом:

```bash
curl -X POST "https://api.tranify.ru/api/v1/auth/token-exchange" \
  -H "Content-Type: application/json" \
  -d '{"token":"<token-from-development-link>"}'
```

Потом:

```bash
curl "https://api.tranify.ru/api/v1/me" \
  -H "Authorization: Bearer <access_token>"
```

## 4. Endpoint'ы

### 4.1. Health

#### `GET /api/v1/health`

Назначение:

- проверить, что API поднят
- получить краткий operational snapshot

Auth:

- не требуется

Пример запроса:

```bash
curl "https://api.tranify.ru/api/v1/health"
```

Пример ответа:

```json
{
  "status": "ok",
  "demoMode": false,
  "invitedUsers": 1,
  "knownUsers": 1,
  "pendingMessages": 0,
  "extractedItems": 76,
  "activeSessions": 3,
  "aiModel": "deepseek-chat",
  "bridgeBot": "@telegrambot:matrix.tranify.ru"
}
```

Поля ответа:

- `status` — сейчас строка `"ok"`
- `demoMode` — включён ли bootstrap/demo seed mode
- `invitedUsers` — сколько email разрешено в pilot allowlist
- `knownUsers` — сколько пользователей знает система
- `pendingMessages` — сколько сообщений ещё не обработано ingestion/extraction pipeline
- `extractedItems` — сколько извлечённых сигналов есть в системе
- `activeSessions` — сколько активных API sessions сейчас живо
- `aiModel` — имя LLM-модели из конфига
- `bridgeBot` — Matrix user id bridge-бота

Статусы:

- `200 OK`

---

### 4.2. Auth

#### `POST /api/v1/auth/magic-links`

Назначение:

- запросить magic link для входа

Auth:

- не требуется

Тело запроса:

```json
{
  "email": "pilot@example.com"
}
```

Поля:

- `email` — email пользователя

Успешный ответ:

```json
{
  "accepted": true,
  "message": "Magic link created.",
  "developmentLink": "https://app.tranify.ru/auth/verify?token=<token>"
}
```

Поля ответа:

- `accepted` — был ли запрос принят
- `message` — человекочитаемое описание результата
- `developmentLink` — dev/pilot ссылка, из которой можно достать одноразовый token

Ошибки:

- если `email` пустой:

```json
{
  "errors": {
    "email": [
      "Email is required."
    ]
  }
}
```

- если запрос отклонён бизнес-логикой:
  - `403 Forbidden`
  - `ProblemDetails` с `title = "Magic link request rejected"`

Статусы:

- `202 Accepted`
- `400 Bad Request` через `ValidationProblem`, если поле не заполнено
- `403 Forbidden`

#### `POST /api/v1/auth/token-exchange`

Назначение:

- обменять одноразовый magic token на bearer session token

Auth:

- не требуется

Тело запроса:

```json
{
  "token": "<magic-link-token>"
}
```

Успешный ответ:

```json
{
  "accessToken": "3b46f7a4d3574e57a34de3d807f2a0a1",
  "tokenType": "Bearer",
  "expiresAt": "2026-04-13T09:01:12+00:00",
  "user": {
    "id": "2fbc4d7a-10d9-4cf1-8ba8-8d6d64f0f6f1",
    "email": "pilot@example.com"
  }
}
```

Поля ответа:

- `accessToken` — bearer token для последующих запросов
- `tokenType` — сейчас всегда `"Bearer"`
- `expiresAt` — момент истечения session token
- `user.id` — внутренний `Guid` пользователя
- `user.email` — email пользователя

Ошибки:

- если `token` пустой:

```json
{
  "errors": {
    "token": [
      "Token is required."
    ]
  }
}
```

- если token невалиден или истёк:
  - `400 Bad Request`
  - `ProblemDetails` с `title = "Token exchange failed"`

Статусы:

- `200 OK`
- `400 Bad Request`

#### `POST /api/v1/auth/refresh`

Назначение:

- перевыпустить текущую bearer session

Auth:

- требуется bearer token

Тело запроса:

- отсутствует

Успешный ответ:

```json
{
  "accessToken": "new_access_token",
  "tokenType": "Bearer",
  "expiresAt": "2026-04-13T09:05:00+00:00",
  "user": {
    "id": "2fbc4d7a-10d9-4cf1-8ba8-8d6d64f0f6f1",
    "email": "pilot@example.com"
  }
}
```

Что делает endpoint:

- достаёт пользователя по текущему bearer token
- отзывает текущий token
- создаёт новый
- возвращает новую session

Статусы:

- `200 OK`
- `401 Unauthorized`

#### `POST /api/v1/auth/logout`

Назначение:

- завершить текущую API session

Auth:

- требуется bearer token

Тело запроса:

- отсутствует

Поведение:

- если bearer token есть, он отзывается
- потом сервер возвращает пустой успешный ответ

Статусы:

- `204 No Content`
- `401 Unauthorized`

---

### 4.3. Текущий пользователь

#### `GET /api/v1/me`

Назначение:

- вернуть краткий профиль авторизованного пользователя
- показать состояние Telegram-подключения

Auth:

- требуется bearer token

Пример ответа:

```json
{
  "id": "2fbc4d7a-10d9-4cf1-8ba8-8d6d64f0f6f1",
  "email": "pilot@example.com",
  "matrixUserId": "@superchat-pilot:matrix.tranify.ru",
  "telegramState": "Connected",
  "lastSyncedAt": "2026-03-14T08:55:12+00:00",
  "requiresTelegramAction": false
}
```

Поля ответа:

- `id` — внутренний `Guid` пользователя
- `email` — email пользователя
- `matrixUserId` — скрытая Matrix identity пользователя, если уже есть
- `telegramState` — текущее состояние Telegram integration
- `lastSyncedAt` — когда Telegram/Matrix синхронизация в последний раз отмечалась успешной
- `requiresTelegramAction` — нужен ли ещё шаг со стороны пользователя

Статусы:

- `200 OK`
- `401 Unauthorized`

---

### 4.4. Интеграции

Общий route prefix:

```text
/api/v1/integrations
```

Поддерживаемые route segments на уровне URL:

- `telegram`
- `whatsapp`
- `signal`
- `discord`
- `slack`
- `email`
- `vk`
- `max`

Важно:

- на уровне маршрутов эти провайдеры распознаются
- но на текущий момент реально реализован только `telegram`
- для остальных API сейчас возвращает `501 Not Implemented`

#### Модель `IntegrationConnectionResponse`

Все общие integration endpoint'ы возвращают объект такого вида:

```json
{
  "provider": "telegram",
  "transport": "MatrixBridge",
  "state": "Connected",
  "matrixUserId": "@superchat-pilot:matrix.tranify.ru",
  "actionUrl": "https://bridge.tranify.ru/public/login?token=...",
  "lastSyncedAt": "2026-03-14T08:55:12+00:00",
  "requiresAction": false
}
```

Поля:

- `provider` — route segment провайдера
- `transport` — тип транспорта: `MatrixBridge`, `DirectApi`, `ImapSmtp`, `Webhook`
- `state` — состояние подключения: `NotStarted`, `Pending`, `Connected`, `RequiresSetup`, `Disconnected`, `Error`
- `matrixUserId` — Matrix identity, если интеграция использует Matrix bridge
- `actionUrl` — ссылка на следующий шаг, если он нужен
- `lastSyncedAt` — время последней синхронизации
- `requiresAction` — нужен ли ещё дополнительный шаг

#### `GET /api/v1/integrations`

Назначение:

- вернуть список integration connections пользователя

Auth:

- требуется bearer token

Важно:

- сейчас список фактически содержит только Telegram connection

Пример ответа:

```json
[
  {
    "provider": "telegram",
    "transport": "MatrixBridge",
    "state": "Connected",
    "matrixUserId": "@superchat-pilot:matrix.tranify.ru",
    "actionUrl": null,
    "lastSyncedAt": "2026-03-14T08:55:12+00:00",
    "requiresAction": false
  }
]
```

Статусы:

- `200 OK`
- `401 Unauthorized`

#### `GET /api/v1/integrations/{provider}`

Назначение:

- вернуть состояние одной конкретной интеграции

Auth:

- требуется bearer token

Path parameter:

- `provider` — один из route segments выше

Поведение:

- если route segment вообще неизвестен, будет `404 Not Found`
- если segment известен, но провайдер ещё не реализован, будет `501 Not Implemented`

Статусы:

- `200 OK`
- `401 Unauthorized`
- `404 Not Found`
- `501 Not Implemented`

#### `POST /api/v1/integrations/{provider}/connect`

Назначение:

- начать подключение интеграции

Auth:

- требуется bearer token

Тело запроса:

- отсутствует

Поведение:

- по текущему email находится `AppUser`
- запускается provider-specific connect flow
- возвращается обновлённый status объекта интеграции

Статусы:

- `200 OK`
- `401 Unauthorized`
- `404 Not Found`
- `501 Not Implemented`

#### `DELETE /api/v1/integrations/{provider}`

Назначение:

- отключить интеграцию

Auth:

- требуется bearer token

Тело запроса:

- отсутствует

Статусы:

- `200 OK`
- `401 Unauthorized`
- `404 Not Found`
- `501 Not Implemented`

---

### 4.5. Telegram

Есть и общий integrations API, и отдельные Telegram-specific endpoint'ы.

Общий route prefix:

```text
/api/v1/integrations/telegram
```

#### Модель `TelegramConnectionResponse`

```json
{
  "state": "Connected",
  "matrixUserId": "@superchat-pilot:matrix.tranify.ru",
  "webLoginUrl": "https://bridge.tranify.ru/public/login?token=...",
  "lastSyncedAt": "2026-03-14T08:55:12+00:00",
  "requiresAction": false
}
```

Поля:

- `state` — состояние подключения Telegram
- `matrixUserId` — Matrix identity пользователя
- `webLoginUrl` — ссылка на bridge web-login flow, если она нужна
- `lastSyncedAt` — время последней синхронизации
- `requiresAction` — нужен ли ещё пользовательский шаг

#### `GET /api/v1/integrations/telegram`

Назначение:

- получить текущий статус Telegram-подключения

Auth:

- требуется bearer token

Статусы:

- `200 OK`
- `401 Unauthorized`

#### `POST /api/v1/integrations/telegram/connect`

Назначение:

- начать или продолжить подключение Telegram

Auth:

- требуется bearer token

Тело запроса:

- отсутствует

Поведение:

- находит текущего пользователя
- запускает Telegram connection flow
- возвращает актуальный `TelegramConnectionResponse`

Статусы:

- `200 OK`
- `401 Unauthorized`

#### `DELETE /api/v1/integrations/telegram`

Назначение:

- отключить Telegram для текущего пользователя

Auth:

- требуется bearer token

Тело запроса:

- отсутствует

Статусы:

- `200 OK`
- `401 Unauthorized`

---

### 4.6. Chat

Общий route prefix:

```text
/api/v1/chat
```

#### `POST /api/v1/chat/ask`

Назначение:

- отправить пользовательский вопрос в chat/product intelligence слой

Auth:

- требуется bearer token

Тело запроса:

```json
{
  "templateId": "today",
  "question": "Что для меня важно сегодня?"
}
```

Поля:

- `templateId` — тип шаблона
- `question` — сам вопрос пользователя

Поддерживаемые `templateId`:

- `today`
- `waiting`
- `meetings`
- `recent`
- `custom`

Важно:

- `custom` поддерживается на уровне API
- но сейчас скрыт из UI каталога шаблонов

Ограничения:

- `question` обязателен
- `question` после `Trim()` не может быть пустым
- максимальная длина — `100` символов
- если `templateId` не поддерживается, будет ошибка валидации через `ArgumentException`

Успешный ответ:

```json
{
  "mode": "today",
  "question": "Что для меня важно сегодня?",
  "items": [
    {
      "title": "Нужно ответить Ивану",
      "summary": "Напомни, пожалуйста, по договору.",
      "sourceRoom": "Иван (Telegram)",
      "timestamp": "2026-03-14T08:10:00+00:00"
    }
  ],
  "assistantText": "Сегодня главное — не забыть ответить Ивану по договору."
}
```

Поля ответа:

- `mode` — нормализованный режим ответа
- `question` — исходный вопрос
- `items` — evidence/results list
- `assistantText` — LLM-enhanced или шаблонный итоговый ответ

Модель `ChatResultItemViewModel`:

- `title` — краткий заголовок
- `summary` — основная выжимка
- `sourceRoom` — человекочитаемое имя чата, если удалось резолвить
- `timestamp` — релевантное время элемента

Ошибки:

- если вопрос длиннее 100 символов:

```json
{
  "errors": {
    "question": [
      "Question must be 100 characters or fewer."
    ]
  }
}
```

Статусы:

- `200 OK`
- `400 Bad Request`
- `401 Unauthorized`

---

### 4.7. Dashboard

Общий route prefix:

```text
/api/v1/dashboard
```

Все dashboard endpoint'ы возвращают `List<DashboardCardViewModel>`.

Модель `DashboardCardViewModel`:

```json
{
  "title": "Нужно ответить Ивану",
  "summary": "Напомни, пожалуйста, по договору.",
  "kind": "WaitingOn",
  "observedAt": "2026-03-14T08:10:00+00:00",
  "dueAt": "2026-03-14T08:10:00+00:00",
  "sourceRoom": "Иван (Telegram)"
}
```

Поля:

- `title` — название карточки
- `summary` — краткое описание
- `kind` — тип карточки
- `observedAt` — когда сигнал был замечен
- `dueAt` — дедлайн или время события, если есть
- `sourceRoom` — имя чата-источника

#### `GET /api/v1/dashboard/today`

Назначение:

- получить карточки для режима “что важно сегодня”

Auth:

- требуется bearer token

Статусы:

- `200 OK`
- `401 Unauthorized`

#### `GET /api/v1/dashboard/waiting`

Назначение:

- получить карточки “кто ждёт моего ответа”

Auth:

- требуется bearer token

Статусы:

- `200 OK`
- `401 Unauthorized`

#### `GET /api/v1/dashboard/meetings`

Назначение:

- получить ближайшие встречи

Auth:

- требуется bearer token

Статусы:

- `200 OK`
- `401 Unauthorized`

---

### 4.8. Search

Общий route prefix:

```text
/api/v1/search
```

#### `GET /api/v1/search?q=<query>`

Назначение:

- выполнить рабочий поиск по extracted items
- если сигналов не найдено, сделать fallback в сырые недавние сообщения

Auth:

- требуется bearer token

Query parameters:

- `q` — строка поиска

Пример:

```bash
curl "https://api.tranify.ru/api/v1/search?q=%D0%B4%D0%BE%D0%B3%D0%BE%D0%B2%D0%BE%D1%80" \
  -H "Authorization: Bearer <access_token>"
```

Успешный ответ:

```json
[
  {
    "title": "Нужно ответить Ивану",
    "summary": "Напомни, пожалуйста, по договору.",
    "kind": "WaitingOn",
    "sourceRoom": "Иван (Telegram)",
    "observedAt": "2026-03-14T08:10:00+00:00"
  }
]
```

Модель `SearchResultViewModel`:

- `title` — заголовок результата
- `summary` — текст результата
- `kind` — тип найденной сущности или `"Message"` для raw fallback
- `sourceRoom` — имя чата
- `observedAt` — timestamp результата

Важное поведение:

- если `q` после `Trim()` пустой, сервис вернёт пустой список
- сначала ищется по `extracted_items`
- если ничего не найдено, поиск делается по последним raw messages

Статусы:

- `200 OK`
- `401 Unauthorized`

---

### 4.9. Feedback

Общий route prefix:

```text
/api/v1/feedback
```

#### `POST /api/v1/feedback`

Назначение:

- записать feedback пользователя по ответу, экрану или зоне интерфейса

Auth:

- требуется bearer token

Тело запроса:

```json
{
  "area": "dashboard.today",
  "useful": true,
  "note": "Это действительно помогло."
}
```

Поля:

- `area` — зона интерфейса или feature area
- `useful` — был ли ответ полезен
- `note` — произвольный комментарий пользователя

Успешный ответ:

```json
{
  "status": "recorded"
}
```

Статусы:

- `202 Accepted`
- `401 Unauthorized`

## 5. Полные примеры

### 5.1. Полный auth flow

#### Шаг 1. Запросить magic link

```bash
curl -X POST "https://api.tranify.ru/api/v1/auth/magic-links" \
  -H "Content-Type: application/json" \
  -d '{"email":"pilot@example.com"}'
```

#### Шаг 2. Обменять token на bearer access token

```bash
curl -X POST "https://api.tranify.ru/api/v1/auth/token-exchange" \
  -H "Content-Type: application/json" \
  -d '{"token":"<magic_token>"}'
```

#### Шаг 3. Вызвать защищённый endpoint

```bash
curl "https://api.tranify.ru/api/v1/me" \
  -H "Authorization: Bearer <access_token>"
```

### 5.2. Спросить chat endpoint

```bash
curl -X POST "https://api.tranify.ru/api/v1/chat/ask" \
  -H "Authorization: Bearer <access_token>" \
  -H "Content-Type: application/json" \
  -d '{
    "templateId": "meetings",
    "question": "Какие у меня ближайшие встречи?"
  }'
```

### 5.3. Получить Telegram status

```bash
curl "https://api.tranify.ru/api/v1/integrations/telegram" \
  -H "Authorization: Bearer <access_token>"
```

### 5.4. Запустить поиск

```bash
curl "https://api.tranify.ru/api/v1/search?q=%D1%86%D0%B5%D0%BD%D0%B0" \
  -H "Authorization: Bearer <access_token>"
```

## 6. Что сейчас важно знать клиенту

### 6.1. Реально реализованные feature groups

Сейчас в боевом смысле уже работают:

- `health`
- `auth`
- `me`
- `telegram integration`
- `chat`
- `dashboard`
- `search`
- `feedback`

### 6.2. Что в integrations ещё зарезервировано, но не реализовано

Эти route segments уже распознаются, но пока вернут `501 Not Implemented`:

- `whatsapp`
- `signal`
- `discord`
- `slack`
- `email`
- `vk`
- `max`

### 6.3. Что важно про chat

- `custom` поддерживается API, но скрыт из UI
- видимые UI-шаблоны сейчас: `today`, `waiting`, `meetings`, `recent`
- API сам нормализует `templateId`
- вопрос длиной больше `100` символов отклоняется

### 6.4. Что важно про ошибки авторизации

Если клиент видит `401 Unauthorized`, это обычно одно из трёх:

- header `Authorization` не передан
- используется не `Bearer`, а другая схема
- session token истёк или уже был отозван

### 6.5. Что важно про production

Актуальный production endpoint:

```text
https://api.tranify.ru/api/v1
```

Health smoke-check:

```bash
curl "https://api.tranify.ru/api/v1/health"
```

## 7. Краткая карта endpoint'ов

```text
GET    /api/v1/health

POST   /api/v1/auth/magic-links
POST   /api/v1/auth/token-exchange
POST   /api/v1/auth/refresh
POST   /api/v1/auth/logout

GET    /api/v1/me

GET    /api/v1/integrations
GET    /api/v1/integrations/{provider}
POST   /api/v1/integrations/{provider}/connect
DELETE /api/v1/integrations/{provider}

GET    /api/v1/integrations/telegram
POST   /api/v1/integrations/telegram/connect
DELETE /api/v1/integrations/telegram

POST   /api/v1/chat/ask

GET    /api/v1/dashboard/today
GET    /api/v1/dashboard/waiting
GET    /api/v1/dashboard/meetings

GET    /api/v1/search?q=<query>

POST   /api/v1/feedback
```

## 8. На что опирается эта документация

Документ составлен по текущему коду API-хоста и его smoke tests:

- [Program.cs](/d:/projects/super-chat/src/SuperChat.Api/Program.cs)
- [AuthEndpoints.cs](/d:/projects/super-chat/src/SuperChat.Api/Features/Auth/AuthEndpoints.cs)
- [ApiSessionAuthenticationHandler.cs](/d:/projects/super-chat/src/SuperChat.Api/Features/Auth/ApiSessionAuthenticationHandler.cs)
- [HealthEndpoints.cs](/d:/projects/super-chat/src/SuperChat.Api/Features/Health/HealthEndpoints.cs)
- [MeEndpoints.cs](/d:/projects/super-chat/src/SuperChat.Api/Features/Me/MeEndpoints.cs)
- [IntegrationEndpoints.cs](/d:/projects/super-chat/src/SuperChat.Api/Features/Integrations/IntegrationEndpoints.cs)
- [TelegramEndpoints.cs](/d:/projects/super-chat/src/SuperChat.Api/Features/Integrations/Telegram/TelegramEndpoints.cs)
- [ChatEndpoints.cs](/d:/projects/super-chat/src/SuperChat.Api/Features/Chat/ChatEndpoints.cs)
- [DashboardEndpoints.cs](/d:/projects/super-chat/src/SuperChat.Api/Features/Dashboard/DashboardEndpoints.cs)
- [SearchEndpoints.cs](/d:/projects/super-chat/src/SuperChat.Api/Features/Search/SearchEndpoints.cs)
- [FeedbackEndpoints.cs](/d:/projects/super-chat/src/SuperChat.Api/Features/Feedback/FeedbackEndpoints.cs)
- [ApiSmokeTests.cs](/d:/projects/super-chat/tests/SuperChat.Api.Tests/ApiSmokeTests.cs)
