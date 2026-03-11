#!/usr/bin/env bash
set -euo pipefail

if [ $# -ne 1 ]; then
  echo "usage: restore-postgres.sh <backup-file.sql>" >&2
  exit 1
fi

ROOT_DIR=$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)
BACKUP_FILE=$1

cd "$ROOT_DIR"
docker compose --env-file .env.prod exec -T postgres \
  psql -U "$POSTGRES_SUPERUSER" -d postgres < "$BACKUP_FILE"
