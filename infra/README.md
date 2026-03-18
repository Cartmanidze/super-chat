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

The local Compose stack now also includes:

- `qdrant`, which is used as the stage-1 retrieval index
- `embedding-service`, an optional Python sidecar for stage-3 text embeddings

PostgreSQL stays the source of truth for chunks and logs.

## VPS pilot bootstrap

1. Copy `infra/prod/.env.example` to `infra/prod/.env`.
2. Fill Matrix, Telegram, SMTP, DeepSeek, and Postgres secrets.
   - for Telegram outgoing-message sync from the logged-in account, also set `MAUTRIX_DOUBLE_PUPPET_AS_TOKEN`
3. Install `envsubst`, for example via `gettext-base`.
4. Run `bash infra/prod/scripts/render-configs.sh`.
5. Run `bash infra/prod/scripts/preflight.sh`.
6. Run `bash infra/prod/scripts/migrate-db.sh`.
7. Run `bash infra/prod/scripts/deploy.sh`.
8. Verify the dedicated bridge host serves the mautrix public site under `/public/`.

## GitHub Actions deploy

The CI workflow can deploy the app layer automatically after a successful `main` build. The deploy job pushes the checked-out commit into the server bare repository, runs `bash infra/prod/scripts/migrate-db.sh`, and then runs `bash infra/prod/scripts/deploy-app.sh` on the VPS.

The app workflow now skips its production deploy step when the pushed change requires a full-stack rollout. This avoids double-deploying the same commit when `deploy-full-stack.yml` is already responsible for the change.

Both production workflows also retry their SSH-backed `git push` and remote deploy steps. This is important on hosts that see background SSH scanner noise, because `sshd` can temporarily throttle new pre-auth connections via `MaxStartups`.

There is now also a separate GitHub workflow for full-stack deploys:

- `.github/workflows/deploy-full-stack.yml`
- it can be started manually via `workflow_dispatch`
- it can auto-run on `main` when infra paths change
- it runs `bash infra/prod/scripts/deploy.sh`, so the whole production compose stack is recreated
- use this path for changes under `infra/prod/**`, `infra/embedding-service/**`, and other changes that add or reshape compose services

Required GitHub `production` environment secrets:

- `PROD_SSH_PRIVATE_KEY`: private key allowed in `authorized_keys` on the production host
- `PROD_SSH_KNOWN_HOSTS`: pinned `known_hosts` entry for the production host, for example from `ssh-keyscan -H <host>`

Recommended GitHub `production` environment variables:

- `PROD_DEPLOY_ENABLED`: set to `true` when the production environment is fully configured and ready for auto-deploy
- `PROD_FULL_STACK_DEPLOY_ENABLED`: set to `true` when infra changes on `main` are allowed to trigger the full-stack workflow automatically
- `PROD_SSH_HOST`: SSH host or IP for the production server
- `PROD_SSH_PORT`: SSH port, defaults to `22`
- `PROD_SSH_USER`: SSH user, defaults to `root`
- `PROD_BARE_REPO_PATH`: bare repo path, defaults to `/opt/super-chat-origin.git`
- `PROD_WORKTREE_PATH`: working tree path, defaults to `/opt/super-chat`
- `PROD_ENV_FILE`: prod env file path, defaults to `/opt/super-chat/infra/prod/.env`
- `PROD_WEB_URL`: optional environment URL shown in GitHub Actions
- `PROD_WEB_HEALTHCHECK_URL`: web smoke-check URL, defaults to `https://app.tranify.ru/health`
- `PROD_API_HEALTHCHECK_URL`: API smoke-check URL, defaults to `https://api.tranify.ru/api/v1/health`

Important:

- `PROD_DEPLOY_ENABLED` and `PROD_FULL_STACK_DEPLOY_ENABLED` should be repository-level GitHub variables, not only environment-level variables
- secrets such as `PROD_SSH_PRIVATE_KEY` and `PROD_SSH_KNOWN_HOSTS` still belong in the `production` environment
- if GitHub Actions ever starts failing with `kex_exchange_identification` or `Connection reset by peer`, check the VPS `sshd` logs for `MaxStartups throttling`

## Notes

- `infra/prod/` assumes `SuperChat.Web` and `SuperChat.Api` run as separate containers.
- `infra/prod/` expects a dedicated bridge host such as `bridge.example.com` for mautrix public login pages.
- `infra/prod/caddy/Caddyfile.template` is the only source for Caddy config; runtime `Caddyfile` is generated.
- `infra/prod/synapse/homeserver.yaml.template`, `infra/prod/synapse/telegram-registration.yaml.template`, and `infra/prod/mautrix/config.yaml.template` are the source templates; runtime `.yaml` files are generated artifacts.
- `infra/prod/synapse/telegram-doublepuppet-registration.yaml.template` enables mautrix double-puppeting for the homeserver domain, which is required if you want the bridge to mirror the logged-in user's own Telegram messages reliably.
- The automated GitHub deploy updates `superchat-web`, `superchat-api`, and the one-off `superchat-db-migrator` image only; Qdrant, the optional `embedding-service`, Synapse, mautrix-telegram, Postgres, and Caddy stay on the manual/full-stack deploy path.
- This gets the VPS stack running, but the app-side Telegram connect flow is still bootstrap logic until the real management-room and `/sync` integration lands.
- These are still bootstrap templates, not a full hardened ops package with monitoring, backups, or secret rotation.
