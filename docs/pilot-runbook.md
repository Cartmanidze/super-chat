# Pilot Runbook

## Local bootstrap

- Fill root `.env`.
- Start `docker compose -f infra/docker-compose.yml up -d`.
- Run `dotnet test SuperChat.sln -m:1`.
- Run `dotnet run --project src/SuperChat.Web`.
- Run `dotnet run --project src/SuperChat.Api`.
- Verify `/health`.
- Verify `/api/v1/health`.
- Request a magic link with an invited email.
- Verify redirect to `/connect/telegram`.
- Verify API token exchange and `/api/v1/me`.
- Start Telegram connection and confirm sample sync produces cards on `/today`.

## VPS pilot bootstrap

- Copy `infra/prod/.env.example` to `infra/prod/.env`.
- Fill Postgres, Matrix, Telegram bridge, SMTP, and DeepSeek secrets.
- Copy `infra/prod/synapse/telegram-registration.yaml.example` to `infra/prod/synapse/telegram-registration.yaml` and replace the placeholders.
- Verify DNS for `app`, `api`, and `matrix` hosts.
- Run `docker compose --env-file infra/prod/.env -f infra/prod/docker-compose.yml up -d --build`.
- Verify `https://<matrix-host>/_matrix/client/versions`.
- Verify `https://<app-host>/health`.
- Verify `https://<api-host>/api/v1/health`.
- Complete the Telegram bridge login and confirm the first message reaches `Today`.

## Readiness checklist

- Invite-only access is enforced.
- Hidden Matrix identity is created on first successful login.
- Web and API use the same PostgreSQL persistence.
- Telegram connection status is visible in UI and API.
- `Today`, `Waiting`, and `Search` render non-empty output.
- Synapse retention is configured before real chat data lands.
- GitHub CI passes on `main`.

## Next upgrades after bootstrap

1. Replace bootstrap Matrix provisioning with real Synapse Admin API calls.
2. Replace bootstrap bridge completion with management room orchestration and `/sync`.
3. Move short-lived sessions and rate limiting to Redis where horizontal scaling needs it.
4. Add backups, monitoring, and secret rotation for the VPS stack.
