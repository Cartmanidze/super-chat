#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR=$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)
ENV_FILE="$ROOT_DIR/.env"

usage() {
  cat <<'EOF'
Usage:
  inspect-telegram-portals.sh [--env-file PATH] <query>

Query rules:
  - digits only: exact Telegram peer id match (`tgid`)
  - starts with `!`: exact Matrix room id match (`mxid`)
  - anything else: fuzzy match by portal title or room id
EOF
}

while [ $# -gt 0 ]; do
  case "$1" in
    --env-file)
      if [ $# -lt 2 ]; then
        echo "Missing value for --env-file" >&2
        usage >&2
        exit 1
      fi

      ENV_FILE="$2"
      shift 2
      ;;
    --help|-h)
      usage
      exit 0
      ;;
    *)
      break
      ;;
  esac
done

QUERY="${1:-}"
if [ -z "$QUERY" ]; then
  usage >&2
  exit 1
fi

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

sql_escape() {
  printf "%s" "$1" | sed "s/'/''/g"
}

sql_list() {
  local values=()
  local value=""
  for value in "$@"; do
    values+=("'$(sql_escape "$value")'")
  done

  local joined=""
  local item=""
  for item in "${values[@]}"; do
    if [ -n "$joined" ]; then
      joined+=", "
    fi

    joined+="$item"
  done

  printf "%s" "$joined"
}

POSTGRES_CONTAINER=$(docker compose --env-file "$ENV_FILE" -f "$ROOT_DIR/docker-compose.yml" ps -q postgres)
if [ -z "$POSTGRES_CONTAINER" ]; then
  echo "Could not resolve postgres container from docker compose." >&2
  exit 1
fi

run_psql() {
  local database="$1"
  local sql="$2"
  docker exec -i "$POSTGRES_CONTAINER" psql -U "$POSTGRES_USER" -d "$database" -P pager=off -c "$sql"
}

run_psql_at() {
  local database="$1"
  local sql="$2"
  docker exec -i "$POSTGRES_CONTAINER" psql -U "$POSTGRES_USER" -d "$database" -At -F $'\t' -c "$sql"
}

escaped_query=$(sql_escape "$QUERY")
where_clause=""
if [[ "$QUERY" =~ ^[0-9]+$ ]]; then
  where_clause="tgid = $QUERY"
elif [[ "$QUERY" == '!'* ]]; then
  where_clause="mxid = '$escaped_query'"
else
  where_clause="title ilike '%' || '$escaped_query' || '%' or mxid ilike '%' || '$escaped_query' || '%'"
fi

mapfile -t portal_rows < <(
  run_psql_at mautrix_telegram "
    select mxid, tgid::text, peer_type, coalesce(title, ''), coalesce(first_event_id, '')
    from portal
    where $where_clause
    order by tgid, mxid;
  "
)

if [ "${#portal_rows[@]}" -eq 0 ]; then
  echo "No portal rows matched query: $QUERY"
  exit 0
fi

room_ids=()
for row in "${portal_rows[@]}"; do
  IFS=$'\t' read -r mxid _tgid _peer_type _title _first_event_id <<< "$row"
  room_ids+=("$mxid")
done

room_list=$(sql_list "${room_ids[@]}")

echo "=== Portal matches ==="
run_psql mautrix_telegram "
  select
    mxid,
    tgid,
    peer_type,
    title,
    first_event_id
  from portal
  where mxid in ($room_list)
  order by tgid, mxid;
"

echo
echo "=== Synapse room members ==="
run_psql synapse "
  select
    room_id,
    user_id,
    membership
  from local_current_membership
  where room_id in ($room_list)
    and membership = 'join'
  order by room_id, user_id;
"

mapfile -t local_users < <(
  run_psql_at synapse "
    select distinct user_id
    from local_current_membership
    where room_id in ($room_list)
      and membership = 'join'
      and user_id like '@superchat-%'
    order by user_id;
  "
)

if [ "${#local_users[@]}" -gt 0 ]; then
  local_user_list=$(sql_list "${local_users[@]}")

  echo
  echo "=== SuperChat local users ==="
  run_psql superchat_app "
    select
      mi.matrix_user_id,
      au.email,
      coalesce(tc.state, '<none>') as telegram_state,
      tc.management_room_id,
      tc.last_synced_at
    from matrix_identities mi
    join app_users au on au.id = mi.user_id
    left join telegram_connections tc on tc.user_id = mi.user_id
    where mi.matrix_user_id in ($local_user_list)
    order by mi.matrix_user_id;
  "
fi

echo
echo "=== Ingestion visibility ==="
run_psql superchat_app "
  select
    matrix_room_id,
    count(*) as normalized_messages,
    max(sent_at) as last_message_at,
    bool_or(not processed) as has_unprocessed
  from normalized_messages
  where matrix_room_id in ($room_list)
  group by matrix_room_id
  order by normalized_messages desc, last_message_at desc;
"
