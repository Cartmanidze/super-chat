# Pilot Next Steps

## Current state

- App, API, Synapse, Postgres, and Caddy are healthy in production.
- Invite-only access is working and `glebon84@gmail.com` is present in `pilot_invites`.
- The real Matrix orchestration flow is working up to Telegram bridge login:
  - hidden Matrix user is created
  - management room is created
  - `login` is sent to the bridge
  - bridge returns a web login URL
- A production hotfix is in place for `mautrix-telegram` because Synapse returns `m.room.create` without `event_id` for `format=event`.

## Current blocker

- Telegram phone login fails inside `mautrix-telegram` with `ApiIdInvalidError`.
- The remaining issue is invalid `MAUTRIX_TELEGRAM_API_ID` / `MAUTRIX_TELEGRAM_API_HASH` in production.
- A BotFather bot token is not a substitute for these values.

## What remains to do

1. Obtain a valid Telegram `api_id` and `api_hash` from `https://my.telegram.org` or from a teammate who already has a working app pair.
2. Update `MAUTRIX_TELEGRAM_API_ID` and `MAUTRIX_TELEGRAM_API_HASH` in `infra/prod/.env` on the server.
3. Re-render configs with `bash infra/prod/scripts/render-configs.sh infra/prod/.env`.
4. Restart the bridge with `docker compose restart mautrix-telegram` in `infra/prod`.
5. Retry the Telegram login flow from `https://app.tranify.ru/connect/telegram` and confirm that phone code delivery succeeds.
6. Verify that the bridge stores the authorized session and that Telegram sync resumes without new bridge exceptions.
7. Commit and push the local production infra changes so the hotfix is not left only on the server.
8. Convert `/opt/super-chat` on the server into a normal git checkout, otherwise future `git pull` deploys will keep being manual.

## Follow-up safety tasks

- Revoke and rotate the BotFather token that was exposed during troubleshooting.
- Consider adding a retry path in the Telegram connect flow if the bridge session expires after the first successful link generation.
