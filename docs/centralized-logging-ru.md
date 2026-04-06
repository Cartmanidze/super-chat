# Централизованное логирование для SuperChat

## Что выбрано

Для этого проекта я выбрал такой стек:

- `Serilog` в `SuperChat.Api`, `SuperChat.Worker`, `SuperChat.DbMigrator`
- `Fluent Bit` как агент сбора из Docker
- `Loki` как центральное хранилище и индекс для логов
- `Grafana` как UI и алертинг

## Почему этот стек

Почему не `ELK`:

- он тяжелее по памяти и диску;
- для нашего Docker-стека это лишняя сложность;
- у нас уже есть `Grafana`, значит половина UI уже готова.

Почему не `Seq`:

- он очень хорош для .NET, но у нас есть не только .NET;
- нужно собирать ещё `nginx`, `caddy`, `synapse`, `mautrix`, `postgres` и остальные контейнеры.

Почему не `Graylog`:

- тоже рабочий вариант, но по числу компонентов и админке он тут избыточен;
- в текущем проекте проще и дешевле поддерживать `Loki + Grafana`.

Итог:

- `Serilog` даёт структурированный JSON в приложении;
- `Fluent Bit` собирает все Docker-логи в одном месте;
- `Loki` быстро фильтрует по low-cardinality labels: `service`, `environment`, `level`;
- `Grafana` уже есть в проекте, поэтому поиск и алерты ложатся в существующую инфраструктуру.

## Как это теперь устроено

Сценарий такой:

1. `API`, `Worker` и `DbMigrator` пишут JSON в stdout.
2. `nginx` и `caddy` тоже пишут JSON access logs.
3. Остальные контейнеры тоже собираются через Docker log driver.
4. Docker добавляет к каждой записи attrs:
   - `com.superchat.service`
   - `SUPERCHAT_LOG_ENVIRONMENT`
5. `Fluent Bit` читает Docker log files в реальном времени.
6. Если строка уже JSON, `Fluent Bit` парсит её и отправляет в `Loki`.
7. В `Loki` stream labels такие:
   - `job=superchat`
   - `service`
   - `environment`
   - `stream`
   - `level` для JSON-логов приложения
8. В `Grafana` подключён datasource `Loki`, а алерты идут через встроенный alerting.

## Что уже внесено в репозиторий

### Приложение

- `API`, `Worker`, `DbMigrator` переведены на `Serilog`
- формат логов: JSON
- добавлен `X-Correlation-ID`
- если заголовок пришёл снаружи, он сохраняется
- если не пришёл, генерируется из `TraceIdentifier`
- request log для `/health` и `/metrics` понижен до `Debug`, чтобы не засорять прод

### Инфраструктура

- добавлен `loki` в [infra/prod/docker-compose.yml](/d:/projects/super-chat/infra/prod/docker-compose.yml)
- добавлен `fluent-bit` в [infra/prod/docker-compose.yml](/d:/projects/super-chat/infra/prod/docker-compose.yml)
- добавлен конфиг `Loki` в [infra/prod/loki/loki-config.yml](/d:/projects/super-chat/infra/prod/loki/loki-config.yml)
- добавлен конфиг `Fluent Bit` в [infra/prod/fluent-bit/fluent-bit.conf](/d:/projects/super-chat/infra/prod/fluent-bit/fluent-bit.conf)
- добавлен datasource `Loki` в [infra/prod/grafana/provisioning/datasources/loki.yml](/d:/projects/super-chat/infra/prod/grafana/provisioning/datasources/loki.yml)
- добавлены правила алертов в [infra/prod/grafana/provisioning/alerting/logging-rules.yml](/d:/projects/super-chat/infra/prod/grafana/provisioning/alerting/logging-rules.yml)
- добавлены contact point и policy для уведомлений

### Веб-сервисы

- `nginx` теперь пишет JSON access log в stdout
- `caddy` теперь пишет JSON access log через `format json`

## Разделение test и prod

Разделение сделано через label `environment`.

Источник значения:

- env переменная `SUPERCHAT_LOG_ENVIRONMENT`

Рекомендуемые значения:

- `test`
- `prod`

Это даёт:

- быстрый переключатель контекста в `Grafana Explore`
- отдельные retention rules в `Loki`
- отдельные алерты по лейблу окружения

## Retention

Сейчас настроено так:

- `test`: 7 дней
- `prod`: 30 дней

Настройка лежит в [infra/prod/loki/loki-config.yml](/d:/projects/super-chat/infra/prod/loki/loki-config.yml) через `retention_stream`.

## Что можно искать

Примеры запросов в `Grafana Explore`:

Все логи API в prod:

```logql
{job="superchat", environment="prod", service="superchat-api"}
```

Только ошибки и фаталы:

```logql
{job="superchat", environment="prod", service="superchat-api", level=~"Error|Fatal"}
```

По correlation id:

```logql
{job="superchat", environment="prod"} | json | Properties_CorrelationId="test-correlation-id"
```

По stack trace или тексту исключения:

```logql
{job="superchat", environment="prod", service="superchat-api"} |~ "SqlException|TimeoutException|deadlock"
```

По кастомным полям из scope:

```logql
{job="superchat", environment="prod"} | json | Properties_PipelineUserId="..."
```

## Алерты

Сейчас добавлены два базовых правила:

- `SuperChat error burst`
  - если больше 5 записей `Error/Fatal` за 5 минут
- `SuperChat fatal exception`
  - если есть хотя бы одна `Fatal` за 1 минуту

Webhook для уведомлений задаётся через:

- `LOG_ALERT_WEBHOOK_URL`

Это лежит в:

- [infra/prod/grafana/provisioning/alerting/logging-contact-points.yml](/d:/projects/super-chat/infra/prod/grafana/provisioning/alerting/logging-contact-points.yml)

## Как развернуть

Если менялись только app-контейнеры, этого мало. Для логирования менялась infra-конфигурация, значит нужен полный путь:

```bash
bash infra/prod/scripts/preflight.sh
bash infra/prod/scripts/deploy.sh
```

После деплоя проверить:

1. `docker compose ps`
2. `https://grafana...` открывается
3. datasource `Loki` виден в Grafana
4. в Explore есть потоки с label `job="superchat"`
5. запрос по `environment="prod"` возвращает логи
6. тестовый `X-Correlation-ID` виден в JSON-логах API

## Как сделать один общий UI для test и prod

Текущая конфигурация уже готова к этому логически:

- логи размечены `environment`
- retention уже разделён
- поиск и алерты уже понимают окружение

Чтобы реально свести оба окружения в одну `Grafana`, нужен один общий `Loki` backend.

Практически это делается так:

1. Оставляете `Grafana + Loki` на одном хосте, обычно на `prod`.
2. На `test` оставляете только агент отправки логов.
3. Агент `test` отправляет логи в тот же `Loki`.
4. В UI переключение идёт по label `environment`.

В этом репозитории уже есть всё, что нужно на уровне формата и меток. Остаётся только выбрать сетевой путь до общего `Loki`.

## Ограничения

Есть одно честное ограничение:

- `.NET`, `nginx` и `caddy` уже пишут аккуратный JSON;
- сторонние сервисы вроде `synapse` и `mautrix` сейчас в основном текстовые.

Но даже для них уже есть централизованный сбор, `service/environment` labels и общий поиск в `Loki`.

Если захотите, следующим шагом можно отдельно перевести ещё и `mautrix/synapse` на более строгий JSON-формат, если их контейнеры это позволят без лишних патчей.
