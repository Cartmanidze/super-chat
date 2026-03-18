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

compose_base=(docker compose --env-file "$ENV_FILE" -f "$ROOT_DIR/docker-compose.yml")
compose_ops=(docker compose --profile ops --env-file "$ENV_FILE" -f "$ROOT_DIR/docker-compose.yml")

"${compose_base[@]}" up -d postgres

postgres_container=$("${compose_base[@]}" ps -q postgres)
if [ -z "$postgres_container" ]; then
  echo "Postgres container is not available." >&2
  exit 1
fi

for attempt in $(seq 1 60); do
  status=$(docker inspect -f '{{if .State.Health}}{{.State.Health.Status}}{{else}}{{.State.Status}}{{end}}' "$postgres_container")
  if [ "$status" = "healthy" ] || [ "$status" = "running" ]; then
    break
  fi

  if [ "$attempt" -eq 60 ]; then
    echo "Postgres did not become ready in time (last status: $status)." >&2
    exit 1
  fi

  sleep 2
done

"${compose_base[@]}" pull superchat-db-migrator
"${compose_ops[@]}" run --rm superchat-db-migrator
