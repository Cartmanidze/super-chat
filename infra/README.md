# infra

This folder now has two tracks:

- `infra/docker-compose.yml`: local bootstrap for development
- `infra/prod/`: VPS-oriented templates for the pilot stack

## Local bootstrap

1. Copy root `.env.example` to `.env`.
2. Start infra with `docker compose -f infra/docker-compose.yml up -d`.
3. Run `dotnet run --project src/SuperChat.Web`.
4. Run `dotnet run --project src/SuperChat.Api`.

The local stack now initializes three PostgreSQL databases through `infra/postgres/init/01-create-databases.sh`:

- `superchat_app`
- `synapse`
- `mautrix_telegram`

## VPS pilot bootstrap

1. Copy `infra/prod/.env.example` to `infra/prod/.env`.
2. Fill Matrix, Telegram, SMTP, DeepSeek, and Postgres secrets.
3. Copy `infra/prod/synapse/homeserver.yaml.example` to `infra/prod/synapse/homeserver.yaml` and render the placeholders.
4. Generate or fill `infra/prod/synapse/telegram-registration.yaml` from the example template.
5. Copy `infra/prod/mautrix/config.yaml.example` to `infra/prod/mautrix/config.yaml` and render the placeholders.
6. Start the pilot stack with `docker compose --env-file infra/prod/.env -f infra/prod/docker-compose.yml up -d --build`.

## Notes

- `infra/prod/` assumes `SuperChat.Web` and `SuperChat.Api` run as separate containers.
- `infra/prod/Caddyfile` expects dedicated hosts for app, api, and matrix.
- These are still bootstrap templates, not a full hardened ops package with monitoring, backups, or secret rotation.
