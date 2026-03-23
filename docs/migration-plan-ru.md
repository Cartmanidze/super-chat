# План переноса Super Chat на новый хостинг

> Дата составления: 2026-03-22
> Причина: DPI-throttling российского хостера замедляет MTProto handshake с Telegram (40ms TCP → 60+ сек криптообмен)

## 1. Текущее состояние сервера

| Параметр | Значение |
|----------|----------|
| IP | 37.46.135.36 |
| Хостер | FVDS (Россия) |
| OS | Ubuntu 24.04.3 LTS |
| CPU | 1 vCPU (AMD EPYC 7662) |
| RAM | 1.8 GB (swap 900 MB активно используется) |
| Диск | 40 GB SSD (74% занято, 28 GB использовано) |
| Docker images | ~19 GB (9 GB reclaimable) |
| Build cache | ~12 GB (11 GB reclaimable) |

### Контейнеры (13 шт.)

| Контейнер | Образ |
|-----------|-------|
| superchat-web | ghcr.io/cartmanidze/superchat-web |
| superchat-api | ghcr.io/cartmanidze/superchat-api |
| mautrix-telegram | prod-mautrix-telegram (локальная сборка) |
| mautrix-telegram-helper | ghcr.io/cartmanidze/superchat-mautrix-telegram-helper |
| synapse | matrixdotorg/synapse:v1.128.0 |
| postgres | postgres:16 |
| qdrant | qdrant/qdrant:v1.15.4 |
| caddy | caddy:2.8 |
| embedding-service | prod-superchat-embedding (локальная сборка) |
| text-enrichment-service | prod-text-enrichment-service (локальная сборка) |
| prometheus | prom/prometheus:v3.10.0 |
| grafana | grafana/grafana:12.4.1 |
| mtprotoproxy | alexbers/mtprotoproxy (временный, убрать после переезда) |

### Базы данных PostgreSQL

| БД | Размер |
|----|--------|
| synapse | 134 MB |
| mautrix_telegram | 21 MB |
| superchat_app | 10 MB |
| **Итого** | **~165 MB** |

### Volumes

| Volume | Размер |
|--------|--------|
| Qdrant | 316 MB |
| Grafana | 52 MB |
| Caddy data | 18 KB (сертификаты пересоздадутся) |
| PostgreSQL | ~49 MB (отдельно от docker volumes) |

### Домены (все → 37.46.135.36)

- `app.tranify.ru` — Web UI
- `api.tranify.ru` — JSON API
- `matrix.tranify.ru` — Synapse homeserver
- `bridge.tranify.ru` — mautrix-telegram web login

### CI/CD (GitHub Actions)

- Bare repo: `/opt/super-chat-origin.git`
- Worktree: `/opt/super-chat`
- SSH доступ через `PROD_SSH_PRIVATE_KEY` / `PROD_SSH_HOST` в GitHub Secrets/Variables
- Deploy: push → build images → push to GHCR → ssh pull + deploy-app.sh

---

## 2. Выбор нового хостинга

### Требования

| Параметр | Минимум | Рекомендуемо |
|----------|---------|-------------|
| RAM | 2 GB | 4 GB |
| CPU | 1 vCPU | 2 vCPU |
| Диск | 40 GB SSD | 60 GB SSD |
| Локация | Европа | Нидерланды (ближе к Telegram DC2: 149.154.167.51) |
| IPv4 | Да | + IPv6 |
| Трафик | Без DPI-throttling Telegram | — |

### Варианты

| Хостинг | План | CPU | RAM | Диск | Цена |
|---------|------|-----|-----|------|------|
| Hetzner Cloud CX22 | CX22 | 2 vCPU | 4 GB | 40 GB | €4.5/мес |
| Contabo Cloud VPS S | VPS S | 4 vCPU | 8 GB | 50 GB | €5.5/мес |
| Netcup RS 1000 | RS 1000 | 2 vCPU | 2 GB | 40 GB | €3.5/мес |

---

## 3. Что переносить

| Компонент | Размер | Способ переноса | Критичность |
|-----------|--------|----------------|-------------|
| PostgreSQL (3 БД) | ~165 MB | `pg_dumpall` → scp → `psql -f` | КРИТИЧНО |
| Synapse signing key | ~100 bytes | scp | КРИТИЧНО (без него Federation сломается) |
| `.env` секреты | ~3 KB | scp | КРИТИЧНО |
| Qdrant vectors | ~316 MB | rsync volume или переиндексация | Важно (можно пересоздать) |
| Git bare repo | ~50 MB | `git clone --bare` с GitHub | Легко |
| Grafana dashboards | ~52 MB | rsync volume | Желательно |
| Caddy TLS certs | ~20 KB | НЕ НУЖНО — Caddy пересоздаст | — |
| Docker images | ~10 GB | НЕ НУЖНО — скачаются из GHCR | — |

---

## 4. Пошаговый план выполнения

### Фаза 0: Подготовка (за 1 день до миграции)

```bash
# 1. Понизить TTL DNS записей до 60 секунд (в DNS-панели домена)
#    Это ускорит переключение — клиенты будут обновлять DNS каждую минуту

# 2. Сделать полный бэкап текущего сервера
ssh tranify "
  pg_dumpall -U postgres -h localhost > /tmp/superchat_backup_$(date +%Y%m%d).sql
  tar czf /tmp/superchat_volumes_$(date +%Y%m%d).tar.gz \
    /opt/super-chat/infra/prod/.env \
    /opt/super-chat/infra/prod/data/
"

# 3. Сгенерировать SSH ключ для нового сервера
ssh-keygen -t ed25519 -f ~/.ssh/id_ed25519_tranify_new -C "superchat-deploy"
```

### Фаза 1: Подготовка нового сервера (30 мин)

```bash
# 4. Создать VPS (Hetzner / Contabo / Netcup)
# 5. SSH на новый сервер, установить Docker
ssh NEW_SERVER "
  curl -fsSL https://get.docker.com | sh
  apt-get install -y git
"

# 6. Настроить SSH ключ для CI/CD
ssh NEW_SERVER "
  mkdir -p ~/.ssh
  # Добавить public key для деплоя
"

# 7. Склонировать репо
ssh NEW_SERVER "
  git clone --bare https://github.com/cartmanidze/super-chat.git /opt/super-chat-origin.git
  git clone /opt/super-chat-origin.git /opt/super-chat
"
```

### Фаза 2: Перенос данных (15 мин, ДАУНТАЙМ НАЧИНАЕТСЯ)

```bash
# 8. Остановить приложение на СТАРОМ сервере
ssh tranify "
  cd /opt/super-chat
  docker compose -f infra/prod/docker-compose.yml stop superchat-web superchat-api mautrix-telegram
"

# 9. Дамп PostgreSQL
ssh tranify "
  docker compose -f /opt/super-chat/infra/prod/docker-compose.yml exec -T postgres \
    pg_dumpall -U postgres > /tmp/superchat_migration.sql
"

# 10. Скопировать данные на новый сервер
scp tranify:/tmp/superchat_migration.sql NEW_SERVER:/tmp/
scp tranify:/opt/super-chat/infra/prod/.env NEW_SERVER:/opt/super-chat/infra/prod/.env

# 11. Скопировать Synapse signing key (КРИТИЧНО!)
# Найти его:
ssh tranify "find / -name '*.signing.key' 2>/dev/null"
# Скопировать:
scp tranify:/path/to/matrix.tranify.ru.signing.key NEW_SERVER:/opt/super-chat/infra/prod/data/synapse/

# 12. Скопировать Qdrant данные
ssh tranify "docker run --rm -v prod_qdrant-data:/data -v /tmp:/backup alpine tar czf /backup/qdrant.tar.gz /data"
scp tranify:/tmp/qdrant.tar.gz NEW_SERVER:/tmp/
ssh NEW_SERVER "docker run --rm -v prod_qdrant-data:/data -v /tmp:/backup alpine sh -c 'cd / && tar xzf /backup/qdrant.tar.gz'"

# 13. Скопировать Grafana данные (опционально)
ssh tranify "docker run --rm -v prod_grafana-data:/data -v /tmp:/backup alpine tar czf /backup/grafana.tar.gz /data"
scp tranify:/tmp/grafana.tar.gz NEW_SERVER:/tmp/
```

### Фаза 3: Запуск на новом сервере (20 мин)

```bash
# 14. Обновить .env на новом сервере
ssh NEW_SERVER "
  cd /opt/super-chat
  # Обновить хосты (если IP изменился в конфиге)
  # Убрать MTProxy (не нужен в Европе!):
  sed -i '/MAUTRIX_TELEGRAM_PROXY/d' infra/prod/.env

  # Обновить connection string если нужно
"

# 15. Поднять PostgreSQL и загрузить дамп
ssh NEW_SERVER "
  cd /opt/super-chat
  docker compose -f infra/prod/docker-compose.yml up -d postgres
  sleep 10
  docker compose -f infra/prod/docker-compose.yml exec -T postgres psql -U postgres < /tmp/superchat_migration.sql
"

# 16. Рендер конфигов и запуск всего стека
ssh NEW_SERVER "
  cd /opt/super-chat
  bash infra/prod/scripts/render-configs.sh infra/prod/.env
  docker compose -f infra/prod/docker-compose.yml up -d
"

# 17. Проверить health
curl --retry 10 --retry-delay 5 https://NEW_IP:443/health --resolve 'app.tranify.ru:443:NEW_IP'
curl --retry 10 --retry-delay 5 https://NEW_IP:443/api/v1/health --resolve 'api.tranify.ru:443:NEW_IP'
```

### Фаза 4: Переключение DNS (5 мин)

```
18. В DNS-панели домена tranify.ru обновить A-записи:
    app.tranify.ru    → НОВЫЙ_IP
    api.tranify.ru    → НОВЫЙ_IP
    matrix.tranify.ru → НОВЫЙ_IP
    bridge.tranify.ru → НОВЫЙ_IP

19. Дождаться пропагации (TTL = 60 сек, если понизили заранее)
```

### Фаза 5: Обновление CI/CD (10 мин)

```
20. GitHub → Repository Settings → Variables (environment: production):
    PROD_SSH_HOST     → НОВЫЙ_IP

21. GitHub → Repository Settings → Secrets:
    PROD_SSH_PRIVATE_KEY  → новый приватный ключ
    PROD_SSH_KNOWN_HOSTS  → ssh-keyscan НОВЫЙ_IP

22. Тестовый deploy:
    GitHub → Actions → ci → Run workflow
```

### Фаза 6: Пост-миграция

```bash
# 23. Перелогиниться в Telegram bridge
#     (новая Telethon сессия — старая привязана к IP старого сервера)

# 24. Проверить работу:
#     - Matrix sync работает
#     - Сообщения приходят из Telegram
#     - Digest/Search/Chat отвечают
#     - AI extraction работает

# 25. Удалить MTProxy конфиг из config.yaml.template
#     (в коде проекта, закоммитить)

# 26. Очистить Docker на новом сервере
ssh NEW_SERVER "docker system prune -af --volumes"

# 27. Старый сервер: НЕ УДАЛЯТЬ 3 дня (на случай отката)
```

---

## 5. Что изменится в коде

После успешной миграции — один коммит:

1. **Убрать proxy из `infra/prod/mautrix/config.yaml.template`** — удалить секцию `proxy:`
2. **Убрать `MAUTRIX_TELEGRAM_PROXY_*` из `.env.example`** (если есть)
3. **Обновить документацию** — IP сервера в docs

---

## 6. Оценка даунтайма

| Этап | Время |
|------|-------|
| Остановка приложения + дамп БД | 5 мин |
| Перенос данных (scp ~500 MB) | 5 мин |
| Запуск на новом сервере | 10 мин |
| DNS пропагация | 1-60 мин (зависит от TTL) |
| **Итого** | **~20-30 мин + DNS** |

**Совет:** Если понизить TTL до 60 сек за день до миграции — пропагация займёт ~1-2 минуты.

---

## 7. Риски и митигация

| Риск | Вероятность | Митигация |
|------|------------|-----------|
| Потеря Synapse signing key | Низкая | Бэкап перед переносом. Без него Synapse не запустится с теми же room ID |
| DNS кэш у клиентов | Средняя | Понизить TTL за день до миграции |
| Telegram FloodWait при перелогине | Средняя | Подождать, повторить через 1-2 часа |
| Старые API сессии клиентов | Нет риска | Токены в БД, БД переехала |
| Embedding model не загрузится | Низкая | embedding-service скачает модель при старте (~1 GB) |
| Matrix federation broken | Низкая | Signing key + server_name сохранены |

---

## 8. Чеклист готовности

- [ ] Новый VPS создан и доступен по SSH
- [ ] Docker + Docker Compose установлены
- [ ] SSH ключ для CI/CD настроен
- [ ] TTL DNS понижен до 60 сек
- [ ] Полный бэкап старого сервера сделан
- [ ] Synapse signing key найден и скопирован
- [ ] PostgreSQL дамп создан и загружен
- [ ] Qdrant данные перенесены
- [ ] .env секреты скопированы и обновлены
- [ ] Конфиги отрендерены
- [ ] Все контейнеры запущены
- [ ] Health endpoints отвечают 200
- [ ] DNS записи обновлены
- [ ] CI/CD переключен на новый сервер
- [ ] Telegram bridge перелогинен
- [ ] Сообщения приходят
- [ ] Старый сервер остановлен (не удалён)
