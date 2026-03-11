#!/usr/bin/env bash
set -euo pipefail

if [ $# -ne 1 ]; then
  echo "usage: restore-postgres.sh <backup-file.sql>" >&2
  exit 1
fi

ROOT_DIR=$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)
BACKUP_FILE=$1

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
  psql -U "$POSTGRES_USER" -d postgres < "$BACKUP_FILE"
