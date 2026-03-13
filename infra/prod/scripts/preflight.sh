#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR=$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)
ENV_FILE="${1:-$ROOT_DIR/.env}"

if [ ! -f "$ENV_FILE" ]; then
  echo "Missing env file: $ENV_FILE" >&2
  exit 1
fi

required_vars=(
  POSTGRES_USER
  POSTGRES_PASSWORD
  SUPERCHAT_PERSISTENCE_CONNECTION
  SUPERCHAT_WEB_HOST
  SUPERCHAT_API_HOST
  TELEGRAM_BRIDGE_HOST
  SUPERCHAT_BASE_URL
  MATRIX_SERVER_NAME
  MATRIX_PUBLIC_BASEURL
  MATRIX_SIGNING_KEY_PATH
  MATRIX_REGISTRATION_SHARED_SECRET
  MAUTRIX_AS_TOKEN
  MAUTRIX_HS_TOKEN
  MAUTRIX_TELEGRAM_API_ID
  MAUTRIX_TELEGRAM_API_HASH
  TELEGRAM_BRIDGE_BOT_USER_ID
  TELEGRAM_BRIDGE_WEB_LOGIN_BASE_URL
  CADDY_ACME_EMAIL
  EMAIL_FROM
  EMAIL_SMTP_HOST
  QDRANT_BASE_URL
)

missing=0
while IFS= read -r line || [ -n "$line" ]; do
  line=${line%$'\r'}
  case "$line" in
    ""|\#*)
      continue
      ;;
  esac

  key=${line%%=*}
  value=${line#*=}
  export "$key=$value"
done < "$ENV_FILE"

for var_name in "${required_vars[@]}"; do
  value=${!var_name:-}
  if [ -z "$value" ]; then
    echo "Missing required variable: $var_name" >&2
    missing=1
    continue
  fi

  case "$value" in
    replace-me|change-me|123456|*example.com*)
      echo "Variable $var_name still looks like a placeholder: $value" >&2
      missing=1
      ;;
  esac
done

if [ "$missing" -ne 0 ]; then
  exit 1
fi

"$ROOT_DIR/scripts/render-configs.sh" "$ENV_FILE"
docker compose --env-file "$ENV_FILE" -f "$ROOT_DIR/docker-compose.yml" config >/dev/null

echo "Preflight passed."
