# Pilot Runbook

## Local bootstrap

- Fill root `.env`.
- Start `docker compose -f infra/docker-compose.yml up -d`.
- Run `dotnet test SuperChat.sln -m:1`.
- Run `npm ci && npm run dev` in `src/SuperChat.Frontend`.
- Run `dotnet run --project src/SuperChat.Api`.
- Run `dotnet run --project src/SuperChat.Worker`.
- Verify `/health`.
- Verify `/api/v1/health`.
- Request a magic link with an invited email.
- Verify redirect to `/connect/telegram`.
- Verify API token exchange and `/api/v1/me`.
- Start Telegram connection and confirm sample sync produces cards on `/today`.

## VPS pilot bootstrap

- Copy `infra/prod/.env.example` to `infra/prod/.env`.
- Fill Postgres, Matrix, Telegram bridge, SMTP, and DeepSeek secrets.
- Install `envsubst`, for example via `gettext-base`.
- Verify DNS for `app`, `api`, `matrix`, and `bridge` hosts.
- Run `bash infra/prod/scripts/render-configs.sh`.
- Run `bash infra/prod/scripts/preflight.sh`.
- Run `bash infra/prod/scripts/deploy.sh`.
- Verify `https://<matrix-host>/_matrix/client/versions`.
- Verify `https://<app-host>/health`.
- Verify `https://<api-host>/api/v1/health`.
- Verify `https://<bridge-host>/public/`.
- Verify Synapse and bridge containers are healthy.
- Treat Telegram connect as infrastructure-only smoke for now; the real management-room login and live `/sync` ingestion are still the next implementation step.

## Readiness checklist

- Invite-only access is enforced.
- Hidden Matrix identity is created on first successful login.
- Frontend, API, and worker use the same PostgreSQL-backed state.
- Telegram connection status is visible in UI and API.
- Meetings and search render non-empty output.
- Synapse retention is configured before real chat data lands.
- GitHub CI passes on `main`.

## Next upgrades after bootstrap

1. Replace bootstrap Matrix provisioning with real Synapse Admin API calls.
2. Replace bootstrap bridge completion with management room orchestration and `/sync`.
3. Move short-lived sessions and rate limiting to Redis where horizontal scaling needs it.
4. Add backups, monitoring, and secret rotation for the VPS stack.
