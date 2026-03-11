#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR=$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)
ENV_FILE="${1:-$ROOT_DIR/.env}"

if [ ! -f "$ENV_FILE" ]; then
  echo "Missing env file: $ENV_FILE" >&2
  exit 1
fi

if ! command -v envsubst >/dev/null 2>&1; then
  echo "envsubst is required. Install gettext before running this script." >&2
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
  export "$key=$value"
done < "$ENV_FILE"

mkdir -p "$ROOT_DIR/caddy" "$ROOT_DIR/synapse" "$ROOT_DIR/mautrix"

envsubst < "$ROOT_DIR/caddy/Caddyfile.template" > "$ROOT_DIR/caddy/Caddyfile"
envsubst < "$ROOT_DIR/synapse/homeserver.yaml.template" > "$ROOT_DIR/synapse/homeserver.yaml"
envsubst < "$ROOT_DIR/synapse/telegram-registration.yaml.template" > "$ROOT_DIR/synapse/telegram-registration.yaml"
envsubst < "$ROOT_DIR/mautrix/config.yaml.template" > "$ROOT_DIR/mautrix/config.yaml"

echo "Rendered production configs into:"
echo "  $ROOT_DIR/caddy/Caddyfile"
echo "  $ROOT_DIR/synapse/homeserver.yaml"
echo "  $ROOT_DIR/synapse/telegram-registration.yaml"
echo "  $ROOT_DIR/mautrix/config.yaml"
