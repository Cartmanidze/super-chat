#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR=$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)
STAMP=$(date -u +%Y%m%dT%H%M%SZ)
BACKUP_DIR="$ROOT_DIR/backups/postgres"
mkdir -p "$BACKUP_DIR"

cd "$ROOT_DIR"
docker compose --env-file .env.prod exec -T postgres \
  pg_dumpall -U "$POSTGRES_SUPERUSER" > "$BACKUP_DIR/$STAMP.sql"

echo "Backup written to $BACKUP_DIR/$STAMP.sql"
