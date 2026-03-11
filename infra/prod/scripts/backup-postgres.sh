#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR=$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)
STAMP=$(date -u +%Y%m%dT%H%M%SZ)
BACKUP_DIR="$ROOT_DIR/backups/postgres"
mkdir -p "$BACKUP_DIR"

if [ ! -f "$ROOT_DIR/.env" ]; then
  echo "Missing $ROOT_DIR/.env" >&2
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
done < "$ROOT_DIR/.env"

docker compose --env-file "$ROOT_DIR/.env" -f "$ROOT_DIR/docker-compose.yml" exec -T postgres \
  pg_dumpall -U "$POSTGRES_USER" > "$BACKUP_DIR/$STAMP.sql"

echo "Backup written to $BACKUP_DIR/$STAMP.sql"
