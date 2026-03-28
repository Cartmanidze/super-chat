#!/usr/bin/env bash
set -euo pipefail

if [ "$#" -eq 0 ]; then
  echo "usage: retry.sh <command> [args...]" >&2
  exit 64
fi

attempts="${RETRY_ATTEMPTS:-6}"
delay_seconds="${RETRY_INITIAL_DELAY_SECONDS:-10}"
backoff_factor="${RETRY_BACKOFF_FACTOR:-2}"

for ((attempt = 1; attempt <= attempts; attempt++)); do
  status=0
  "$@" || status=$?

  if [ "$status" -eq 0 ]; then
    exit 0
  fi

  if [ "$attempt" -ge "$attempts" ]; then
    echo "Command failed after ${attempts} attempts." >&2
    exit "$status"
  fi

  echo "Command failed with exit code ${status}. Retrying in ${delay_seconds}s (attempt ${attempt}/${attempts})..." >&2
  sleep "$delay_seconds"
  delay_seconds=$((delay_seconds * backoff_factor))
done
