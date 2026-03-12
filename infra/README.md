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
7. Verify the dedicated bridge host serves the mautrix public site under `/public/`.

## GitHub Actions deploy

The CI workflow can deploy the app layer automatically after a successful `main` build. The deploy job pushes the checked-out commit into the server bare repository and then runs `bash infra/prod/scripts/deploy-app.sh` on the VPS, so only `superchat-web` and `superchat-api` are rebuilt automatically.

Required GitHub `production` environment secrets:

- `PROD_SSH_PRIVATE_KEY`: private key allowed in `authorized_keys` on the production host
- `PROD_SSH_KNOWN_HOSTS`: pinned `known_hosts` entry for the production host, for example from `ssh-keyscan -H <host>`

Recommended GitHub `production` environment variables:

- `PROD_DEPLOY_ENABLED`: set to `true` when the production environment is fully configured and ready for auto-deploy
- `PROD_SSH_HOST`: SSH host or IP for the production server
- `PROD_SSH_PORT`: SSH port, defaults to `22`
- `PROD_SSH_USER`: SSH user, defaults to `root`
- `PROD_BARE_REPO_PATH`: bare repo path, defaults to `/opt/super-chat-origin.git`
- `PROD_WORKTREE_PATH`: working tree path, defaults to `/opt/super-chat`
- `PROD_ENV_FILE`: prod env file path, defaults to `/opt/super-chat/infra/prod/.env`
- `PROD_WEB_URL`: optional environment URL shown in GitHub Actions
- `PROD_WEB_HEALTHCHECK_URL`: web smoke-check URL, defaults to `https://app.tranify.ru/health`
- `PROD_API_HEALTHCHECK_URL`: API smoke-check URL, defaults to `https://api.tranify.ru/api/v1/health`

## Notes

- `infra/prod/` assumes `SuperChat.Web` and `SuperChat.Api` run as separate containers.
- `infra/prod/` expects a dedicated bridge host such as `bridge.example.com` for mautrix public login pages.
- `infra/prod/caddy/Caddyfile.template` is the only source for Caddy config; runtime `Caddyfile` is generated.
- `infra/prod/synapse/homeserver.yaml.template`, `infra/prod/synapse/telegram-registration.yaml.template`, and `infra/prod/mautrix/config.yaml.template` are the source templates; runtime `.yaml` files are generated artifacts.
- The automated GitHub deploy updates `superchat-web` and `superchat-api` only; Synapse, mautrix-telegram, Postgres, and Caddy stay on the manual/full-stack deploy path.
- This gets the VPS stack running, but the app-side Telegram connect flow is still bootstrap logic until the real management-room and `/sync` integration lands.
- These are still bootstrap templates, not a full hardened ops package with monitoring, backups, or secret rotation.
