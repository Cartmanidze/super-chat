#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR=$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)
ENV_FILE="${1:-$ROOT_DIR/.env}"

"$ROOT_DIR/scripts/preflight.sh" "$ENV_FILE"
docker compose --env-file "$ENV_FILE" -f "$ROOT_DIR/docker-compose.yml" up -d --build --no-deps superchat-web superchat-api
