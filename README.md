# super-chat

`super-chat` is a bootstrap for an AI layer over Telegram via Matrix. This repo now ships two hosts on top of the same domain/infrastructure core: `SuperChat.Web` for the product UI and `SuperChat.Api` for mobile-facing and machine-facing JSON integration.

## What is included

- Invite-only magic link auth flow
- Separate `/api/v1/*` host for future mobile clients
- Hidden Matrix identity bootstrap per user
- Telegram connection bootstrap flow with a development bridge stub
- PostgreSQL-backed auth, connection state, message normalization, extraction, digest, search, and feedback paths
- Stage-1 retrieval foundation with PostgreSQL chunk/log tables and Qdrant collection bootstrap
- Docker Compose skeleton for Postgres, Qdrant, an optional embedding sidecar, Synapse, mautrix-telegram, Caddy, and Mailpit
- Production bootstrap under `infra/prod/` for VPS pilot deployment
- GitHub Actions CI/CD for build, tests, and production app deploy on `main`

## Documentation

- English short architecture note: `docs/architecture.md`
- Russian detailed guide: `docs/project-guide-ru.md`
- Russian target architecture for multi-channel expansion: `docs/multichannel-architecture-ru.md`
- Russian target architecture for AI retrieval and summaries: `docs/ai-retrieval-architecture-ru.md`

## Quick start

1. Copy `.env.example` to `.env` and fill the secrets you already have.
2. Start the infra stack from `infra/`.
3. Run `dotnet test SuperChat.sln -m:1`.
4. Run `dotnet run --project src/SuperChat.Web`.
5. Run `dotnet run --project src/SuperChat.Api`.
6. Open the web app or call the API with one of the emails from `SuperChat__AllowedEmails`.

Key API routes in bootstrap mode:

- `POST /api/v1/auth/magic-links`
- `POST /api/v1/auth/token-exchange`
- `POST /api/v1/auth/refresh`
- `GET /api/v1/me`
- `GET|POST|DELETE /api/v1/integrations/telegram`
- `GET /api/v1/dashboard/today`
- `GET /api/v1/dashboard/waiting`
- `GET /api/v1/search?q=...`
- `POST /api/v1/feedback`
- `GET /api/v1/health`

In development, the requested magic link is returned directly in responses and the Telegram connect flow can seed demo messages so the `Today`, `Waiting`, and `Search` surfaces show product value before the real bridge is wired. By default the app can still fall back to EF Core in-memory storage, but the provided `.env.example` now points local bootstrap at PostgreSQL so `SuperChat.Web` and `SuperChat.Api` share the same state.

Three pilot-specific knobs now live under `SuperChat` config as well:

- `EnableGroupIngestion` controls whether group chats are ingested at all. Direct chats still work even when it is disabled. Default: `false`.
- `MaxIngestedGroupMembers` controls which Telegram group rooms are ingested. Direct chats are still allowed, and group rooms are limited by participant count. Default: `30`.
- `TodayTimeZoneId` controls the day boundary for the `Today` digest. Default: `Europe/Moscow`.

Stage-2 retrieval chunking is configured separately under `Chunking`:

- `Enabled` turns the chunk builder worker on or off. Default: `true`.
- `PollSeconds` controls how often the chunk builder looks for new messages. Default: `5`.
- `MaxGapMinutes` controls when a long pause splits chunks. Default: `15`.
- `MaxMessagesPerChunk` caps how many messages go into one chunk. Default: `8`.
- `MaxChunkCharacters` caps rendered chunk size. Default: `1600`.

Stage-3 embeddings are configured under `Embedding` on the .NET side and `EMBEDDING_*` for the optional Python sidecar:

- `Embedding.Enabled` turns the client on or off. Default: `true`.
- `Embedding.Backend` chooses `LocalService` or `YandexCloud`. Default: `LocalService`.
- `Embedding.BaseUrl` points Web/API to the sidecar when `Backend=LocalService`. Local default: `http://localhost:7291`.
- `Embedding.TimeoutSeconds` controls the HTTP timeout to the sidecar. Default: `60`.
- `Embedding.DenseVectorSize` documents the expected dense vector width. Default: `1024`.
- `Embedding.YandexBaseUrl` points to Yandex AI Studio. Default: `https://ai.api.cloud.yandex.net`.
- `Embedding.YandexApiKey` stores the Yandex AI Studio API key.
- `Embedding.YandexFolderId` lets the app build model URIs like `emb://<folder_id>/text-search-doc/latest`.
- `Embedding.YandexDocModelUri` / `Embedding.YandexQueryModelUri` can override the generated model URIs explicitly.
- `Embedding.YandexDocModelName` / `Embedding.YandexQueryModelName` default to `text-search-doc` and `text-search-query`.
- When `Backend=YandexCloud`, set both `Embedding.DenseVectorSize` and `Qdrant.DenseVectorSize` to the Yandex embedding width you use. The standard Yandex search embeddings are currently `256`.
- `EMBEDDING_PROVIDER` chooses `mock` or `bgem3` for the optional Python sidecar. Default: `mock`.
- `EMBEDDING_INSTALL_BGE=1` rebuilds the sidecar image with `FlagEmbedding` so the `bgem3` provider can actually boot.

Stage-4 chunk indexing is configured under `ChunkIndexing`:

- `ChunkIndexing.Enabled` turns the indexing worker on or off. Default: `true`.
- `ChunkIndexing.PollSeconds` controls how often the worker picks pending chunks. Default: `10`.
- `ChunkIndexing.BatchSize` caps how many chunks are embedded and upserted per tick. Default: `20`.

Chunk-based meeting projection is configured under `MeetingProjection`:

- `MeetingProjection.Enabled` turns the chunk-to-meetings worker on or off. Default: `true`.
- `MeetingProjection.PollSeconds` controls how often the worker rebuilds chunk-derived meetings for changed rooms. Default: `15`.

Stage-5 retrieval is configured under `Retrieval`:

- `Retrieval.Enabled` turns the Qdrant-backed retrieval path on or off. Default: `true`.
- `Retrieval.PrefetchLimit` controls how many dense and sparse candidates are gathered before RRF fusion. Default: `24`.
- `Retrieval.ResultLimit` controls how many ranked chunks come back from hybrid retrieval. Default: `8`.

Stage-6 chat answer generation is configured under `ChatAnswering`:

- `ChatAnswering.Enabled` turns the DeepSeek-backed answer synthesis on or off. Default: `true`.
- `ChatAnswering.MaxContextChunks` limits how many retrieved chunks are sent to DeepSeek. Default: `5`.
- `ChatAnswering.MaxEvidenceItems` limits how many evidence cards come back from the AI answer. Default: `3`.
- `ChatAnswering.MaxContextCharacters` caps the total context text sent to DeepSeek. Default: `4800`.
- `ChatAnswering.MaxOutputTokens` caps the JSON answer size requested from DeepSeek. Default: `500`.

