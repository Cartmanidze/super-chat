# Architecture

## Flow

`Telegram -> mautrix-telegram -> Synapse -> SuperChat.Web -> AI extraction -> digest/search UI`

The repository starts as a modular monolith and keeps two clearly separated contours:

- `infra/`: Synapse, mautrix-telegram, Postgres, Mailpit, Caddy
- `src/`: product app, auth, hidden Matrix provisioning, sync bootstrap, extraction, digest, search, feedback

## Bounded modules

- `Auth`: invite-only magic links and pilot access
- `MatrixProvisioning`: hidden Matrix user per product user
- `TelegramConnect`: bridge login orchestration and connection status
- `Sync`: normalized messages with dedup by `matrix_room_id + matrix_event_id`
- `Extraction`: provider abstraction plus DeepSeek-ready seam
- `Digest`: `Today` and `Waiting`
- `Search`: structured search over extracted data
- `Feedback`: pilot usefulness signal

## Key flows

### Magic link

1. User requests a link with an invited email.
2. App issues a short-lived token.
3. Verification signs in the user and ensures Matrix identity exists.

### Telegram connect

1. Signed-in user opens `/connect/telegram`.
2. App shows hidden Matrix user and bridge bootstrap status.
3. In development bootstrap mode the app marks the bridge as connected and seeds sample sync data.

### Sync and extraction

1. `MatrixSyncBackgroundService` seeds or later polls messages.
2. `MessageNormalizationService` deduplicates by room/event key.
3. `ExtractionBackgroundService` converts messages into `task`, `meeting`, `commitment`, `waiting_on`.
4. `Today`, `Waiting`, and `Search` read from the derived layer.

## Current implementation boundary

The repo now implements a working bootstrap. Real Synapse Admin API calls, real bridge management rooms, and real DeepSeek HTTP calls are still intentionally isolated behind services so we can replace development behavior incrementally instead of rewriting the whole app.
