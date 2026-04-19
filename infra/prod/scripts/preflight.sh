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
  SUPERCHAT_BASE_URL
  CADDY_ACME_EMAIL
  EMAIL_FROM
  EMAIL_SMTP_HOST
  QDRANT_BASE_URL
)

optional_vars=(
  TELEGRAM_USERBOT_HMAC_SECRET
  TELEGRAM_SESSION_ENCRYPTION_KEY
  TELEGRAM_API_ID
  TELEGRAM_API_HASH
  TELEGRAM_USERBOT_DATABASE_URL
  MAX_USERBOT_HMAC_SECRET
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
  if [ -z "${!key+x}" ]; then
    export "$key=$value"
  fi
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

if [ "${TELEGRAM_USERBOT_ENABLED:-false}" = "true" ]; then
  for var_name in "${optional_vars[@]}"; do
    case "$var_name" in
      MAX_USERBOT_HMAC_SECRET)
        continue
        ;;
    esac
    value=${!var_name:-}
    if [ -z "$value" ] || [ "$value" = "replace-me" ]; then
      echo "TelegramUserbot enabled but $var_name is empty or a placeholder." >&2
      missing=1
    fi
  done
fi

if [ "${MAX_USERBOT_ENABLED:-false}" = "true" ]; then
  value=${MAX_USERBOT_HMAC_SECRET:-}
  if [ -z "$value" ] || [ "$value" = "replace-me" ]; then
    echo "MaxUserbot enabled but MAX_USERBOT_HMAC_SECRET is empty or a placeholder." >&2
    missing=1
  fi
fi

if [ "$missing" -ne 0 ]; then
  exit 1
fi

"$ROOT_DIR/scripts/render-configs.sh" "$ENV_FILE"
docker compose --env-file "$ENV_FILE" -f "$ROOT_DIR/docker-compose.yml" config >/dev/null

echo "Preflight passed."
