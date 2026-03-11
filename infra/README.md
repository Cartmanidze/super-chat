# infra bootstrap

This folder contains the local and VPS bootstrap for the Matrix/Telegram side of `super-chat`.

## Containers

- `postgres`: app data, Synapse DB, future persistence
- `mailpit`: development SMTP for magic links
- `synapse`: Matrix homeserver
- `mautrix-telegram`: Telegram bridge
- `caddy`: public entrypoint and TLS termination

## What to fill in before first real run

1. `infra/synapse/homeserver.yaml`
2. `infra/mautrix/config.yaml`
3. Root `.env`
4. Telegram `api_id` and `api_hash`
5. Synapse admin token and bridge appservice tokens

## Local bootstrap

1. Start infra: `docker compose -f infra/docker-compose.yml up -d`
2. Open Mailpit at `http://localhost:8025`
3. Run the web app outside Docker with `dotnet run --project src/SuperChat.Web`
4. Point Caddy upstreams to the app and Matrix endpoints you actually use

The compose stack is intentionally a bootstrap, not a production-ready hardened deployment. It exists to get Synapse, the bridge, SMTP, and TLS wiring into one visible place from the first commit.
