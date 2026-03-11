# Pilot Runbook

## Before first internal pilot

- Fill root `.env` and infra templates
- Start `docker compose -f infra/docker-compose.yml up -d`
- Run `dotnet test`
- Run `dotnet run --project src/SuperChat.Web`
- Verify `/health`
- Request a magic link with an invited email
- Verify redirect to `/connect/telegram`
- Start Telegram connection and confirm sample sync produces cards on `/today`
- Submit one feedback entry

## Readiness checklist

- Invite-only access is enforced
- Hidden Matrix identity is created on first successful login
- Telegram connection status is visible in UI
- `Today`, `Waiting`, and `Search` render non-empty output in bootstrap mode
- CI passes on GitHub
- Mailpit receives development emails
- Synapse retention is configured before real chat data lands

## Next upgrades after bootstrap

1. Replace development Matrix provisioning with real Synapse Admin API calls.
2. Replace development bridge completion with management room orchestration and `/sync`.
3. Replace heuristic extraction fallback with real DeepSeek structured extraction.
4. Move persistence from in-memory bootstrap store to PostgreSQL-backed storage.
