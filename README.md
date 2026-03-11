# super-chat

`super-chat` is a bootstrap for an AI layer over Telegram via Matrix. This repo starts as a `.NET 10` modular monolith with Razor Pages, background services, Docker-based infra, a feature-first folder layout, and a clear seam for replacing the AI provider later.

## What is included

- Invite-only magic link auth flow
- Hidden Matrix identity bootstrap per user
- Telegram connection bootstrap flow with a development bridge stub
- In-memory message normalization, extraction, digest, search, and feedback paths
- Docker Compose skeleton for Postgres, Synapse, mautrix-telegram, Caddy, and Mailpit
- CI workflow for build and tests

## Quick start

1. Copy `.env.example` to `.env` and fill the secrets you already have.
2. Start the infra stack from `infra/`.
3. Run `dotnet test SuperChat.sln`.
4. Run `dotnet run --project src/SuperChat.Web`.
5. Open the app and request a magic link for one of the emails from `SUPERCHAT_ALLOWED_EMAILS`.

In development, the requested magic link is shown directly in the UI and the Telegram connect flow can seed demo messages so the `Today`, `Waiting`, and `Search` pages show product value before the real bridge is wired.
