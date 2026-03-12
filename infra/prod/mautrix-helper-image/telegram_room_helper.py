import asyncio
import os
import time
from pathlib import Path
from typing import Any

import psycopg
import yaml
from aiohttp import web
from telethon import TelegramClient, types
from telethon.crypto import AuthKey
from telethon.sessions import MemorySession
from telethon.tl.functions.channels import GetFullChannelRequest
from telethon.tl.functions.messages import GetFullChatRequest


CONFIG_PATH = Path("/data/config.yaml")
CACHE_TTL_SECONDS = 900
LISTEN_HOST = "0.0.0.0"
LISTEN_PORT = 29318


def load_config() -> dict[str, Any]:
    return yaml.safe_load(CONFIG_PATH.read_text(encoding="utf-8"))


def build_session(session_row: tuple[int, str, int, bytes]) -> MemorySession:
    dc_id, server_address, port, auth_key = session_row
    session = MemorySession()
    session.set_dc(dc_id, server_address, port)
    session.auth_key = AuthKey(bytes(auth_key))
    return session


async def fetch_room_info(
    db_dsn: str,
    api_id: int,
    api_hash: str,
    matrix_user_id: str,
    room_id: str,
) -> dict[str, Any] | None:
    with psycopg.connect(db_dsn) as conn:
        with conn.cursor() as cur:
            cur.execute("select tgid, peer_type, title, megagroup from portal where mxid = %s", (room_id,))
            portal = cur.fetchone()
            if portal is None:
                return None

            cur.execute(
                "select dc_id, server_address, port, auth_key from telethon_sessions where session_id = %s",
                (matrix_user_id,),
            )
            session_row = cur.fetchone()
            if session_row is None:
                return None

    tgid, peer_type, title, megagroup = portal
    session = build_session(session_row)
    client = TelegramClient(session, api_id, api_hash)
    await client.connect()

    try:
        participant_count: int | None = None
        is_broadcast_channel = False

        if peer_type == "user":
            participant_count = 2
        elif peer_type == "channel":
            input_entity = await client.get_input_entity(types.PeerChannel(int(tgid)))
            full = await client(GetFullChannelRequest(input_entity))
            participant_count = getattr(full.full_chat, "participants_count", None)
            channel = full.chats[0] if full.chats else None
            title = title or getattr(channel, "title", None)

            if channel is not None:
                if getattr(channel, "broadcast", None) is not None:
                    is_broadcast_channel = bool(channel.broadcast)
                else:
                    is_broadcast_channel = not bool(getattr(channel, "megagroup", megagroup))
            else:
                is_broadcast_channel = not bool(megagroup)
        elif peer_type == "chat":
            full = await client(GetFullChatRequest(int(tgid)))
            participants = getattr(getattr(full.full_chat, "participants", None), "participants", None) or []
            participant_count = len(participants)
            title = title or getattr(full.full_chat, "title", None)

        return {
            "room_id": room_id,
            "peer_type": peer_type,
            "participant_count": participant_count,
            "title": title,
            "is_broadcast_channel": is_broadcast_channel,
        }
    finally:
        await client.disconnect()


async def handle_room_info(request: web.Request) -> web.Response:
    matrix_user_id = request.query.get("matrixUserId", "").strip()
    room_id = request.match_info["room_id"].strip()
    if not matrix_user_id or not room_id:
        return web.json_response({"error": "matrixUserId and room_id are required"}, status=400)

    cache_key = (matrix_user_id, room_id)
    cache = request.app["cache"]
    now = time.monotonic()
    cached = cache.get(cache_key)
    if cached and cached["expires_at"] > now:
        return web.json_response(cached["payload"])

    try:
        payload = await fetch_room_info(
            request.app["db_dsn"],
            request.app["api_id"],
            request.app["api_hash"],
            matrix_user_id,
            room_id,
        )
    except Exception as ex:  # pragma: no cover - operational fallback
        return web.json_response({"error": str(ex)}, status=502)

    if payload is None:
        return web.json_response({"error": "room or session not found"}, status=404)

    cache[cache_key] = {
        "expires_at": now + CACHE_TTL_SECONDS,
        "payload": payload,
    }

    return web.json_response(payload)


def create_app() -> web.Application:
    config = load_config()
    appservice = config.get("appservice", {})
    telegram = config.get("telegram", {})

    app = web.Application()
    app["api_id"] = int(telegram["api_id"])
    app["api_hash"] = str(telegram["api_hash"])
    app["db_dsn"] = str(appservice["database"])
    app["cache"] = {}
    app.router.add_get("/rooms/{room_id}/info", handle_room_info)
    return app


if __name__ == "__main__":
    web.run_app(create_app(), host=LISTEN_HOST, port=LISTEN_PORT)
