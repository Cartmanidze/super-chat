#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR=$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)
ENV_FILE="${1:-$ROOT_DIR/.env}"

if [ ! -f "$ENV_FILE" ]; then
  echo "Missing env file: $ENV_FILE" >&2
  exit 1
fi

while IFS= read -r line || [ -n "$line" ]; do
  line=${line%$'\r'}
  case "$line" in
    ""|\#*)
      continue
      ;;
  esac

  key=${line%%=*}
  value=${line#*=}
  if [ -z "${!key+x}" ]; then
    export "$key=$value"
  fi
done < "$ENV_FILE"

if [ -n "${GHCR_USERNAME:-}" ] && [ -n "${GHCR_TOKEN:-}" ]; then
  printf '%s' "$GHCR_TOKEN" | docker login "${GHCR_REGISTRY:-ghcr.io}" -u "$GHCR_USERNAME" --password-stdin
fi

"$ROOT_DIR/scripts/preflight.sh" "$ENV_FILE"
docker compose --env-file "$ENV_FILE" -f "$ROOT_DIR/docker-compose.yml" pull superchat-web superchat-api superchat-worker
docker compose --env-file "$ENV_FILE" -f "$ROOT_DIR/docker-compose.yml" build mautrix-telegram-helper
docker compose --env-file "$ENV_FILE" -f "$ROOT_DIR/docker-compose.yml" up -d --no-deps caddy mautrix-telegram-helper superchat-web superchat-api superchat-worker
