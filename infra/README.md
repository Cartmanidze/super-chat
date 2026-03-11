# infra

This folder now has two tracks:

- `infra/docker-compose.yml`: local bootstrap for development
- `infra/prod/`: VPS-oriented pilot deployment

## Local bootstrap

1. Copy root `.env.example` to `.env`.
2. Start infra with `docker compose -f infra/docker-compose.yml up -d`.
3. Run `dotnet run --project src/SuperChat.Web`.
4. Run `dotnet run --project src/SuperChat.Api`.

The local stack initializes three PostgreSQL databases through `infra/postgres/init/01-create-databases.sh`:

- `superchat_app`
- `synapse`
- `mautrix_telegram`

## VPS pilot bootstrap

1. Copy `infra/prod/.env.example` to `infra/prod/.env`.
2. Fill Matrix, Telegram, SMTP, DeepSeek, and Postgres secrets.
3. Install `envsubst`, for example via `gettext-base`.
4. Run `bash infra/prod/scripts/render-configs.sh`.
5. Run `bash infra/prod/scripts/preflight.sh`.
6. Run `bash infra/prod/scripts/deploy.sh`.

## Notes

- `infra/prod/` assumes `SuperChat.Web` and `SuperChat.Api` run as separate containers.
- `infra/prod/caddy/Caddyfile.template` is the only source for Caddy config; runtime `Caddyfile` is generated.
- `infra/prod/synapse/homeserver.yaml.template`, `infra/prod/synapse/telegram-registration.yaml.template`, and `infra/prod/mautrix/config.yaml.template` are the source templates; runtime `.yaml` files are generated artifacts.
- This gets the VPS stack running, but the app-side Telegram connect flow is still bootstrap logic until the real management-room and `/sync` integration lands.
- These are still bootstrap templates, not a full hardened ops package with monitoring, backups, or secret rotation.
