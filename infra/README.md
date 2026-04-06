# infra

This folder now has two tracks:

- `infra/docker-compose.yml`: local bootstrap for development
- `infra/prod/`: VPS-oriented pilot deployment

## Local bootstrap

1. Copy root `.env.example` to `.env` if you want your own local overrides.
2. Create the local mautrix config from the template if it does not exist yet:

   ```powershell
   if (-not (Test-Path infra/mautrix/config.yaml)) {
     Copy-Item infra/mautrix/config.yaml.template infra/mautrix/config.yaml
   }
   ```

3. Start the full local stack:

   ```powershell
   docker compose --env-file .env.example -f infra/docker-compose.yml up -d --build
   ```

4. Check the main endpoints:

   ```powershell
   curl http://localhost:15050/api/v1/health
   curl http://localhost:15051/health
   curl http://localhost:8008/health
   ```

5. Open:
   - `https://app.localhost`
   - `http://localhost:15050`
   - `http://localhost:15051`
   - `http://localhost:18025`
6. Stop the stack when needed:

   ```powershell
   docker compose --env-file .env.example -f infra/docker-compose.yml down
   ```

The local stack initializes three PostgreSQL databases through `infra/postgres/init/01-create-databases.sh`:

- `superchat_app`
- `synapse`
- `mautrix_telegram`

The local Compose stack now also includes:

- `qdrant`, which is used as the stage-1 retrieval index
- `embedding-service`, an optional Python sidecar for stage-3 text embeddings
- `superchat-web`, which serves the built React frontend
- `superchat-api`
- `superchat-worker`
- `synapse`
- `mautrix-telegram`
- `mailpit`

PostgreSQL stays the source of truth for chunks and logs.

## VPS pilot bootstrap

1. Copy `infra/prod/.env.example` to `infra/prod/.env`.
2. Fill Matrix, Telegram, SMTP, DeepSeek, and Postgres secrets.
   - for Telegram outgoing-message sync from the logged-in account, also set `MAUTRIX_DOUBLE_PUPPET_AS_TOKEN`
3. Install `envsubst`, for example via `gettext-base`.
4. Run `bash infra/prod/scripts/render-configs.sh`.
5. Run `bash infra/prod/scripts/preflight.sh`.
6. Run `bash infra/prod/scripts/migrate-db.sh`.
   - this starts Postgres and Qdrant if needed, applies EF Core migrations, bootstraps the Qdrant memory collection, and fails the deploy if Qdrant bootstrap cannot complete
7. Run `bash infra/prod/scripts/deploy.sh`.
8. Verify the dedicated bridge host serves the mautrix public site under `/public/`.
9. Open Grafana on `https://<GRAFANA_HOST>` and confirm the provisioned `SuperChat Overview` and `SuperChat Pipeline` dashboards are present.

## GitHub Actions deploy

The CI workflow now treats `tranify` as staging and `super-chat` as production. Staging can deploy from `main` automatically. Production deploy is a separate manual run.

The app workflow now skips its production deploy step when the pushed change requires a full-stack rollout. This avoids double-deploying the same commit when `deploy-full-stack.yml` is already responsible for the change.

Both production workflows also retry their SSH-backed `git push` and remote deploy steps. This is important on hosts that see background SSH scanner noise, because `sshd` can temporarily throttle new pre-auth connections via `MaxStartups`.

There is now also a separate GitHub workflow for full-stack deploys:

- `.github/workflows/deploy-full-stack.yml`
- it can be started manually via `workflow_dispatch`
- it can auto-run on `main` when infra paths change
- it runs `bash infra/prod/scripts/deploy.sh`, so the whole production compose stack is recreated
- use this path for changes under `infra/prod/**`, `infra/embedding-service/**`, and other changes that add or reshape compose services

Required GitHub `staging` environment secrets:

- `STAGING_SSH_PRIVATE_KEY`: private key allowed in `authorized_keys` on the staging host
- `STAGING_SSH_KNOWN_HOSTS`: pinned `known_hosts` entry for the staging host, for example from `ssh-keyscan -H <host>`

Required GitHub `production` environment secrets:

- `PROD_SSH_PRIVATE_KEY`: private key allowed in `authorized_keys` on the production host
- `PROD_SSH_KNOWN_HOSTS`: pinned `known_hosts` entry for the production host, for example from `ssh-keyscan -H <host>`

Recommended GitHub `staging` environment variables:

- `STAGING_DEPLOY_ENABLED`: set to `true` when staging app deploy is ready
- `STAGING_FULL_STACK_DEPLOY_ENABLED`: set to `true` when infra changes on `main` may auto-deploy to staging
- `STAGING_SSH_HOST`: SSH host or IP for the staging server
- `STAGING_SSH_PORT`: SSH port, defaults to `22`
- `STAGING_SSH_USER`: SSH user, defaults to `root`
- `STAGING_BARE_REPO_PATH`: bare repo path, defaults to `/opt/super-chat-origin.git`
- `STAGING_WORKTREE_PATH`: working tree path, defaults to `/opt/super-chat`
- `STAGING_ENV_FILE`: env file path, defaults to `/opt/super-chat/infra/prod/.env`
- `STAGING_WEB_URL`: optional environment URL shown in GitHub Actions
- `STAGING_WEB_HEALTHCHECK_URL`: web smoke-check URL, defaults to `https://app.tranify.ru/health`
- `STAGING_API_HEALTHCHECK_URL`: API smoke-check URL, defaults to `https://api.tranify.ru/api/v1/health`

Recommended GitHub `production` environment variables:

- `PROD_DEPLOY_ENABLED`: set to `true` when manual production deploy is allowed
- `PROD_FULL_STACK_DEPLOY_ENABLED`: set to `true` when manual full-stack production deploy is allowed
- `PROD_SSH_HOST`: SSH host or IP for the production server
- `PROD_SSH_PORT`: SSH port, defaults to `22`
- `PROD_SSH_USER`: SSH user, defaults to `root`
- `PROD_BARE_REPO_PATH`: bare repo path, defaults to `/opt/super-chat-origin.git`
- `PROD_WORKTREE_PATH`: working tree path, defaults to `/opt/super-chat`
- `PROD_ENV_FILE`: prod env file path, defaults to `/opt/super-chat/infra/prod/.env`
- `PROD_WEB_URL`: optional environment URL shown in GitHub Actions
- `PROD_WEB_HEALTHCHECK_URL`: web smoke-check URL, defaults to `https://app.super-chat.org/health`
- `PROD_API_HEALTHCHECK_URL`: API smoke-check URL, defaults to `https://api.super-chat.org/api/v1/health`

Important:

- `STAGING_DEPLOY_ENABLED`, `STAGING_FULL_STACK_DEPLOY_ENABLED`, `PROD_DEPLOY_ENABLED`, and `PROD_FULL_STACK_DEPLOY_ENABLED` should be repository-level GitHub variables, not only environment-level variables
- secrets should stay inside their matching GitHub environments: `staging` and `production`
- if GitHub Actions ever starts failing with `kex_exchange_identification` or `Connection reset by peer`, check the VPS `sshd` logs for `MaxStartups throttling`

## Notes

- `infra/prod/` assumes a static frontend container, a separate API container, and a separate worker container.
- `infra/prod/` expects a dedicated bridge host such as `bridge.example.com` for mautrix public login pages.
- `infra/prod/caddy/Caddyfile.template` is the only source for Caddy config; runtime `Caddyfile` is generated.
- `infra/mautrix/config.yaml.template` is the source for the local mautrix config; keep `infra/mautrix/config.yaml` local-only because the bridge may rewrite secrets into it on startup.
- `infra/prod/prometheus/prometheus.yml` configures scraping for the web host, API host, Prometheus itself, and Qdrant.
- `infra/prod/grafana/provisioning/**` and `infra/prod/grafana/dashboards/**` provision Grafana automatically on container start.
- `infra/prod/synapse/homeserver.yaml.template`, `infra/prod/synapse/telegram-registration.yaml.template`, and `infra/prod/mautrix/config.yaml.template` are the source templates; runtime `.yaml` files are generated artifacts.
- `infra/prod/synapse/telegram-doublepuppet-registration.yaml.template` enables mautrix double-puppeting for the homeserver domain, which is required if you want the bridge to mirror the logged-in user's own Telegram messages reliably.
- The automated GitHub deploy updates the React frontend image behind `superchat-web`, `superchat-api`, `superchat-worker`, and the one-off `superchat-db-migrator` image only; Qdrant, the optional `embedding-service`, Synapse, mautrix-telegram, Postgres, and Caddy stay on the manual/full-stack deploy path, although `migrate-db.sh` now also reuses the running Qdrant service to ensure the target collection exists and stops the deploy when that bootstrap fails.
- This gets the VPS stack running, but the app-side Telegram connect flow is still bootstrap logic until the real management-room and `/sync` integration lands.
- The VPS stack now includes Prometheus and Grafana with provisioned dashboards, but backups and secret rotation are still out of scope.
