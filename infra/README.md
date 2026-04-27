# infra

This folder now has two tracks:

- `infra/docker-compose.yml`: local bootstrap for development
- `infra/prod/`: VPS-oriented pilot deployment

## Local bootstrap

1. Copy root `.env.example` to `.env` if you want your own local overrides.
2. Start the full local stack:

   ```powershell
   docker compose --env-file .env.example -f infra/docker-compose.yml up -d --build
   ```

3. Check the main endpoints:

   ```powershell
   curl http://localhost:15050/api/v1/health
   curl http://localhost:15051/health
   ```

4. Open:
   - `https://app.localhost`
   - `http://localhost:15050`
   - `http://localhost:15051`
   - `http://localhost:18025`
5. Stop the stack when needed:

   ```powershell
   docker compose --env-file .env.example -f infra/docker-compose.yml down
   ```

The local stack initializes the `superchat_app` PostgreSQL database through `infra/postgres/init/01-create-databases.sh`.

The local Compose stack also includes:

- `qdrant`, used as the stage-1 retrieval index
- `embedding-service`, an optional Python sidecar for stage-3 text embeddings
- `superchat-web`, which serves the built React frontend
- `superchat-api`
- `superchat-worker`
- `telegram-userbot-service` and `max-userbot-service`
- `mailpit`

PostgreSQL stays the source of truth for chunks and logs.

## VPS pilot bootstrap

1. Copy `infra/prod/.env.example` to `infra/prod/.env`.
2. Fill Telegram, SMTP, DeepSeek, and Postgres secrets.
3. Install `envsubst`, for example via `gettext-base`.
4. Run `bash infra/prod/scripts/render-configs.sh`.
5. Run `bash infra/prod/scripts/preflight.sh`.
6. Run `bash infra/prod/scripts/migrate-db.sh`.
   - this starts Postgres and Qdrant if needed, applies EF Core migrations, bootstraps the Qdrant memory collection, and fails the deploy if Qdrant bootstrap cannot complete
7. Run `bash infra/prod/scripts/deploy.sh`.
8. Open Grafana on `https://<GRAFANA_HOST>` and confirm the provisioned `SuperChat Overview` and `SuperChat Pipeline` dashboards are present.

## GitHub Actions deploy

There is one production deploy target, triggered manually via `workflow_dispatch`.

The app workflow (`.github/workflows/ci.yml`) builds, tests, and pushes images for any push to the `production` branch. Deploy to production happens only on a manual `workflow_dispatch` and only when the change does not require a full-stack rollout.

Both deploy steps retry their SSH-backed `git push` and remote command. This is important on hosts that see background SSH scanner noise, because `sshd` can temporarily throttle new pre-auth connections via `MaxStartups`.

There is a separate workflow for full-stack deploys:

- `.github/workflows/deploy-full-stack.yml`
- runs only via manual `workflow_dispatch`
- runs `bash infra/prod/scripts/deploy.sh`, so the whole production compose stack is recreated
- use this path for changes under `infra/prod/**`, `infra/embedding-service/**`, and other changes that add or reshape compose services

Required GitHub `production` environment secrets:

- `PROD_SSH_PRIVATE_KEY`: private key allowed in `authorized_keys` on the production host
- `PROD_SSH_KNOWN_HOSTS`: pinned `known_hosts` entry for the production host, for example from `ssh-keyscan -H <host>`

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

- `PROD_DEPLOY_ENABLED` and `PROD_FULL_STACK_DEPLOY_ENABLED` should be repository-level GitHub variables, not only environment-level variables
- secrets should stay inside the `production` GitHub environment
- if GitHub Actions ever starts failing with `kex_exchange_identification` or `Connection reset by peer`, check the VPS `sshd` logs for `MaxStartups throttling`

## Notes

- `infra/prod/` assumes a static frontend container, a separate API container, and a separate worker container.
- `infra/prod/caddy/Caddyfile.template` is the only source for Caddy config; runtime `Caddyfile` is generated.
- `infra/prod/prometheus/prometheus.yml` configures scraping for the web host, API host, Prometheus itself, and Qdrant.
- `infra/prod/grafana/provisioning/**` and `infra/prod/grafana/dashboards/**` provision Grafana automatically on container start.
- The automated GitHub deploy updates `superchat-web`, `superchat-api`, `superchat-worker`, and the one-off `superchat-db-migrator` image only; Qdrant, the optional `embedding-service`, Postgres, and Caddy stay on the manual/full-stack deploy path. `migrate-db.sh` reuses the running Qdrant service to ensure the target collection exists and stops the deploy when that bootstrap fails.
- The VPS stack also includes Prometheus and Grafana with provisioned dashboards, but backups and secret rotation are still out of scope.
