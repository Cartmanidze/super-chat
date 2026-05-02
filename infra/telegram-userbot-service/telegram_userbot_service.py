"""FastAPI service that manages Telethon userbot sessions per SuperChat user.

Endpoints
---------
POST   /sessions/{user_id}/connect     - start login by phone
POST   /sessions/{user_id}/code        - submit SMS code
POST   /sessions/{user_id}/password    - submit 2FA password
POST   /sessions/{user_id}/disconnect  - drop the session
GET    /sessions/{user_id}/status      - report current connection state
GET    /health                         - liveness probe

Inbound Telegram messages are forwarded to the SuperChat worker at
`SUPERCHAT_API_URL/api/v1/internal/telegram/incoming` with an
HMAC-SHA256 signature in the `X-Superchat-Signature` header.

If required credentials are missing (TELEGRAM_API_ID/HASH, encryption key,
HMAC secret), the service boots in `disabled` mode: only /health responds
OK; every `/sessions/*` endpoint returns 503 with `sidecar_disabled`.
This lets the container run in production before the pilot has wired up
secrets, without the restart-loop that a hard crash would cause.
"""
from __future__ import annotations

import asyncio
import hashlib
import hmac
import json
import logging
import os
import time
from contextlib import asynccontextmanager
from dataclasses import dataclass, field
from typing import Optional
from uuid import UUID

import httpx
from fastapi import FastAPI, HTTPException, Path
from pydantic import BaseModel, Field
from telethon import TelegramClient, events
from telethon.errors import (
    PhoneCodeInvalidError,
    PhoneNumberInvalidError,
    SessionPasswordNeededError,
)

from postgres_session import PostgresSession, PostgresSessionStore, SessionCipher

LOG_LEVEL = os.environ.get("LOG_LEVEL", "INFO")
logging.basicConfig(level=LOG_LEVEL, format="%(asctime)s %(levelname)s %(name)s %(message)s")
logger = logging.getLogger("telegram_userbot_service")

DATABASE_URL = os.environ.get("DATABASE_URL", "")
SUPERCHAT_API_URL = os.environ.get("SUPERCHAT_API_URL", "").rstrip("/")
HMAC_SECRET = os.environ.get("HMAC_SECRET", "")
TELEGRAM_API_ID_RAW = os.environ.get("TELEGRAM_API_ID", "0") or "0"
try:
    TELEGRAM_API_ID = int(TELEGRAM_API_ID_RAW)
except ValueError:
    TELEGRAM_API_ID = 0
TELEGRAM_API_HASH = os.environ.get("TELEGRAM_API_HASH", "")

# Период health-check всех живых клиентов. Делаем большим (5 минут), чтобы не
# создавать постоянный фон запросов к Telegram. is_user_authorized() — это
# users.GetUsers([InputUserSelf]) на API, лёгкий, но при тысячах сессий
# суммарный трафик ощутимый. Для пилота 5 минут — компромисс между
# скоростью реакции на отзыв и нагрузкой.
HEALTH_CHECK_INTERVAL_SECONDS = float(
    os.environ.get("HEALTH_CHECK_INTERVAL_SECONDS", "300")
)


@dataclass
class SessionState:
    user_id: UUID
    client: TelegramClient
    phone: Optional[str] = None
    phone_code_hash: Optional[str] = None
    status: str = "not_started"
    telegram_user_id: Optional[int] = None
    # Strong reference to the long-running run_until_disconnected task so the
    # asyncio event loop does not garbage-collect it mid-flight.
    background_task: Optional[asyncio.Task] = field(default=None, repr=False)


class _Registry:
    def __init__(self) -> None:
        self._sessions: dict[UUID, SessionState] = {}
        self._store: Optional[PostgresSessionStore] = None
        self._http: Optional[httpx.AsyncClient] = None
        self._disabled_reason: Optional[str] = None

    def configure(
        self,
        store: Optional[PostgresSessionStore],
        http: Optional[httpx.AsyncClient],
        disabled_reason: Optional[str] = None,
    ) -> None:
        self._store = store
        self._http = http
        self._disabled_reason = disabled_reason

    @property
    def disabled_reason(self) -> Optional[str]:
        return self._disabled_reason

    @property
    def is_enabled(self) -> bool:
        return self._disabled_reason is None and self._store is not None and self._http is not None

    def store(self) -> PostgresSessionStore:
        if self._store is None:
            raise RuntimeError("Session store is not configured.")
        return self._store

    def http(self) -> httpx.AsyncClient:
        if self._http is None:
            raise RuntimeError("HTTP client is not configured.")
        return self._http

    def get(self, user_id: UUID) -> Optional[SessionState]:
        return self._sessions.get(user_id)

    def set(self, state: SessionState) -> None:
        self._sessions[state.user_id] = state

    async def drop(self, user_id: UUID) -> None:
        state = self._sessions.pop(user_id, None)
        if state is None:
            return

        if state.background_task is not None:
            state.background_task.cancel()
            try:
                await state.background_task
            except (asyncio.CancelledError, Exception):
                pass

        try:
            await state.client.disconnect()
        except Exception:
            logger.exception("Failed to disconnect Telethon client for %s", user_id)

    async def close(self) -> None:
        for user_id in list(self._sessions):
            await self.drop(user_id)


registry = _Registry()


class StartConnectRequest(BaseModel):
    phone: str = Field(min_length=5)


class StartConnectResponse(BaseModel):
    status: str
    phone_code_hash: Optional[str] = None


class SubmitCodeRequest(BaseModel):
    code: str


class SubmitCodeResponse(BaseModel):
    status: str


class SubmitPasswordRequest(BaseModel):
    password: str


class SessionStatusResponse(BaseModel):
    status: str
    phone: Optional[str] = None
    telegram_user_id: Optional[int] = None


def _resolve_disabled_reason() -> Optional[str]:
    missing = []
    if TELEGRAM_API_ID == 0:
        missing.append("TELEGRAM_API_ID")
    if not TELEGRAM_API_HASH:
        missing.append("TELEGRAM_API_HASH")
    if not DATABASE_URL:
        missing.append("DATABASE_URL")
    if not os.environ.get("TELEGRAM_SESSION_ENCRYPTION_KEY"):
        missing.append("TELEGRAM_SESSION_ENCRYPTION_KEY")
    if not HMAC_SECRET:
        missing.append("HMAC_SECRET")
    if not SUPERCHAT_API_URL:
        missing.append("SUPERCHAT_API_URL")

    if missing:
        return "missing_env:" + ",".join(missing)
    return None


@asynccontextmanager
async def lifespan(app: FastAPI):
    disabled_reason = _resolve_disabled_reason()
    http: Optional[httpx.AsyncClient] = None
    store: Optional[PostgresSessionStore] = None
    health_check_task: Optional[asyncio.Task] = None

    if disabled_reason is not None:
        logger.warning("Telegram userbot sidecar starts in disabled mode: %s", disabled_reason)
        registry.configure(store=None, http=None, disabled_reason=disabled_reason)
    else:
        try:
            cipher = SessionCipher.from_environment()
        except RuntimeError as exc:
            logger.warning("Session cipher init failed: %s", exc)
            registry.configure(store=None, http=None, disabled_reason=f"cipher_init:{exc}")
        else:
            store = PostgresSessionStore(DATABASE_URL, cipher)
            http = httpx.AsyncClient(timeout=10.0)
            registry.configure(store=store, http=http, disabled_reason=None)
            # Поднимаем сохранённые сессии — иначе после рестарта sidecar
            # никакой пользователь не получит входящих/исходящих сообщений,
            # пока вручную не нажмёт «Переподключить Telegram».
            await _resume_persisted_sessions(store)
            # Стартуем фоновый health-check после resume — чтобы первый
            # тик не пересёкся с восстановлением сессий.
            health_check_task = asyncio.create_task(
                _health_check_loop(HEALTH_CHECK_INTERVAL_SECONDS),
                name="telegram-health-check",
            )

    try:
        yield
    finally:
        if health_check_task is not None:
            health_check_task.cancel()
            try:
                await health_check_task
            except (asyncio.CancelledError, Exception):
                pass
        await registry.close()
        if http is not None:
            await http.aclose()


async def _resume_persisted_sessions(store: PostgresSessionStore) -> None:
    try:
        user_ids = await store.list_user_ids()
    except Exception:
        logger.exception("Failed to enumerate persisted Telegram sessions")
        return

    if not user_ids:
        logger.info("No persisted Telegram sessions to resume on startup")
        return

    logger.info("Resuming %d persisted Telegram session(s) on startup", len(user_ids))
    for user_id in user_ids:
        try:
            await _resume_one_session(user_id, store)
        except Exception:
            logger.exception("Failed to resume Telegram session for %s", user_id)


async def _resume_one_session(user_id: UUID, store: PostgresSessionStore) -> None:
    if registry.get(user_id) is not None:
        return

    # Без restore PostgresSession создаётся пустой — auth_key не загружается из БД
    # и Telethon считает сессию неавторизованной даже при валидной записи.
    session = await PostgresSession.restore(user_id, store)
    client = TelegramClient(session, TELEGRAM_API_ID, TELEGRAM_API_HASH)
    await client.connect()

    if not await client.is_user_authorized():
        # Пользователь либо не довёл логин, либо Telegram отозвал auth_key.
        # Раньше мы просто молча отключались — UI продолжал показывать
        # state=Connected, и пользователь не понимал, почему сообщения не
        # приходят. Теперь уведомляем .NET-worker, чтобы он перевёл состояние
        # в Revoked и UI/мобилка показали «нужен вход».
        logger.warning("Persisted session for %s is not authorized; marking revoked", user_id)
        await client.disconnect()
        await _notify_session_revoked(user_id, reason="not_authorized_on_resume")
        return

    state = SessionState(
        user_id=user_id,
        client=client,
        phone=None,
        phone_code_hash=None,
        status="connected",
    )
    registry.set(state)
    await _complete_connection(state)
    logger.info("Resumed Telegram session for %s", user_id)


async def _notify_session_revoked(user_id: UUID, reason: str) -> None:
    """Сообщает SuperChat-worker, что сессия больше не авторизована.

    Идемпотентно: worker может получать повторные уведомления (например,
    health-check будет звать каждые 5 минут до перезапуска контейнера) —
    он только обновит UpdatedAt и не сломается.
    """
    if registry.disabled_reason is not None:
        return

    if not SUPERCHAT_API_URL or not HMAC_SECRET:
        # На уровне DI в _resolve_disabled_reason это уже проверено, но
        # дополнительная защита от race condition при rolling restart.
        logger.warning(
            "Cannot notify session-revoked for %s: SUPERCHAT_API_URL or HMAC_SECRET missing",
            user_id,
        )
        return

    payload = {
        "user_id": str(user_id),
        "reason": reason,
        "timestamp": int(time.time()),
    }
    body = json.dumps(payload, separators=(",", ":")).encode("utf-8")
    signature = hmac.new(HMAC_SECRET.encode("utf-8"), body, hashlib.sha256).hexdigest()
    headers = {
        "Content-Type": "application/json",
        "X-Superchat-Signature": f"sha256={signature}",
    }

    try:
        response = await registry.http().post(
            f"{SUPERCHAT_API_URL}/api/v1/internal/telegram/session-revoked",
            content=body,
            headers=headers,
        )
        if response.status_code >= 400:
            logger.warning(
                "SuperChat rejected session-revoked notification for %s: %s",
                user_id,
                response.status_code,
            )
    except Exception:
        # Сетевая ошибка не должна валить health-check loop. На следующей
        # итерации worker всё равно попробует снова — сессия не вернётся
        # сама собой, без действия пользователя.
        logger.exception("Failed to notify session-revoked for %s", user_id)


async def _health_check_loop(interval_seconds: float) -> None:
    """Раз в interval_seconds опрашиваем все live-клиенты и проверяем, что
    Telegram всё ещё считает их авторизованными. Если нет — выкидываем из
    registry и сообщаем .NET-worker, чтобы UI показал «нужен вход».

    Запускается из lifespan и живёт всё время работы контейнера. Падение
    одной итерации не валит цикл — ждём и пробуем снова.
    """
    logger.info("Starting Telegram session health-check loop (interval=%.0fs)", interval_seconds)
    while True:
        try:
            await asyncio.sleep(interval_seconds)
            await _probe_all_sessions()
        except asyncio.CancelledError:
            logger.info("Health-check loop cancelled")
            raise
        except Exception:
            logger.exception("Health-check loop iteration failed; will retry next tick")


async def _probe_all_sessions() -> None:
    """Снимок registry на момент вызова — чтобы конкурентные drop/connect
    из endpoints не дрались с итерацией."""
    user_ids = list(registry._sessions.keys())  # snapshot, не view
    if not user_ids:
        return

    for user_id in user_ids:
        state = registry.get(user_id)
        if state is None:
            continue
        try:
            authorized = await state.client.is_user_authorized()
        except Exception:
            logger.exception("is_user_authorized() crashed for %s", user_id)
            continue

        if authorized:
            continue

        logger.warning(
            "Health-check: session for %s is no longer authorized; marking revoked",
            user_id,
        )
        await registry.drop(user_id)
        await _notify_session_revoked(user_id, reason="not_authorized_on_health_check")


app = FastAPI(title="SuperChat Telegram Userbot", lifespan=lifespan)


@app.get("/health")
async def health() -> dict[str, str]:
    if registry.disabled_reason is not None:
        return {"status": "ok", "mode": "disabled", "reason": registry.disabled_reason}
    return {"status": "ok", "mode": "enabled"}


@app.post("/sessions/{user_id}/connect", response_model=StartConnectResponse)
async def connect(user_id: UUID, payload: StartConnectRequest) -> StartConnectResponse:
    _require_enabled()
    state = registry.get(user_id)
    if state is not None and state.status == "connected":
        return StartConnectResponse(status="connected")

    session = PostgresSession(user_id, registry.store())
    client = TelegramClient(session, TELEGRAM_API_ID, TELEGRAM_API_HASH)
    await client.connect()

    try:
        sent = await client.send_code_request(payload.phone)
    except PhoneNumberInvalidError as exc:
        await client.disconnect()
        raise HTTPException(status_code=400, detail="phone_invalid") from exc

    state = SessionState(
        user_id=user_id,
        client=client,
        phone=payload.phone,
        phone_code_hash=sent.phone_code_hash,
        status="awaiting_code",
    )
    registry.set(state)
    return StartConnectResponse(status="awaiting_code", phone_code_hash=sent.phone_code_hash)


@app.post("/sessions/{user_id}/code", response_model=SubmitCodeResponse)
async def submit_code(user_id: UUID, payload: SubmitCodeRequest) -> SubmitCodeResponse:
    _require_enabled()
    state = _require_state(user_id)
    if state.phone is None or state.phone_code_hash is None:
        raise HTTPException(status_code=409, detail="connect_not_started")

    try:
        await state.client.sign_in(
            phone=state.phone,
            code=payload.code,
            phone_code_hash=state.phone_code_hash,
        )
    except PhoneCodeInvalidError as exc:
        raise HTTPException(status_code=400, detail="code_invalid") from exc
    except SessionPasswordNeededError:
        state.status = "awaiting_password"
        return SubmitCodeResponse(status="awaiting_password")

    await _complete_connection(state)
    return SubmitCodeResponse(status="connected")


@app.post("/sessions/{user_id}/password", response_model=SubmitCodeResponse)
async def submit_password(user_id: UUID, payload: SubmitPasswordRequest) -> SubmitCodeResponse:
    _require_enabled()
    state = _require_state(user_id)

    await state.client.sign_in(password=payload.password)
    await _complete_connection(state)
    return SubmitCodeResponse(status="connected")


@app.post("/sessions/{user_id}/disconnect")
async def disconnect(user_id: UUID) -> dict[str, str]:
    if registry.disabled_reason is not None:
        return {"status": "disconnected"}

    state = registry.get(user_id)
    if state is not None and hasattr(state.client.session, "forget"):
        try:
            await state.client.session.forget()
        except Exception:
            logger.exception("Failed to forget Telegram session for %s", user_id)
    await registry.drop(user_id)
    return {"status": "disconnected"}


@app.get("/sessions/{user_id}/status", response_model=SessionStatusResponse)
async def status(user_id: UUID = Path(...)) -> SessionStatusResponse:
    if registry.disabled_reason is not None:
        return SessionStatusResponse(status="not_started")

    state = registry.get(user_id)
    if state is None:
        raise HTTPException(status_code=404, detail="not_found")
    return SessionStatusResponse(
        status=state.status,
        phone=state.phone,
        telegram_user_id=state.telegram_user_id,
    )


async def _complete_connection(state: SessionState) -> None:
    me = await state.client.get_me()
    state.telegram_user_id = getattr(me, "id", None)
    state.status = "connected"

    # Слушаем и входящие (от собеседников), и исходящие (что пользователь сам пишет
    # с любого Telegram-клиента). Без outgoing event-ов SuperChat не видит ответы
    # пользователя, и waiting/commitment/meeting auto-resolution ломаются.
    state.client.add_event_handler(
        _make_message_handler(state.user_id),
        events.NewMessage(),
    )

    # Keep a strong reference so the asyncio event loop does not GC the task.
    state.background_task = asyncio.create_task(
        _run_until_disconnected(state),
        name=f"telethon-client-{state.user_id}",
    )


async def _run_until_disconnected(state: SessionState) -> None:
    try:
        await state.client.run_until_disconnected()
    except asyncio.CancelledError:
        raise
    except Exception:
        logger.exception("Telethon client loop crashed for %s", state.user_id)


def _make_message_handler(user_id: UUID):
    async def handle(event: events.NewMessage.Event) -> None:
        try:
            await _forward_message(user_id, event)
        except Exception:
            logger.exception("Failed to forward incoming message for %s", user_id)

    return handle


async def _forward_message(user_id: UUID, event: events.NewMessage.Event) -> None:
    if registry.disabled_reason is not None:
        return

    message = event.message
    if message is None or not message.message:
        return

    sender = await event.get_sender()
    sender_name = _build_display_name(sender)
    chat = await event.get_chat()
    chat_title = _build_chat_title(chat, fallback=sender_name)
    chat_id = str(event.chat_id)
    message_id = f"{chat_id}:{message.id}"
    is_outgoing = bool(getattr(message, "out", False))

    payload = {
        "user_id": str(user_id),
        "external_chat_id": chat_id,
        "external_message_id": message_id,
        "chat_title": chat_title,
        "sender_name": sender_name,
        "text": message.message,
        "sent_at": message.date.isoformat(),
        "is_outgoing": is_outgoing,
        # Unix-секунды на момент отправки. Worker отбрасывает запросы старше ±5 минут —
        # защита от replay-атак.
        "timestamp": int(time.time()),
    }

    body = json.dumps(payload, separators=(",", ":")).encode("utf-8")
    signature = hmac.new(HMAC_SECRET.encode("utf-8"), body, hashlib.sha256).hexdigest()
    headers = {
        "Content-Type": "application/json",
        "X-Superchat-Signature": f"sha256={signature}",
    }

    response = await registry.http().post(
        f"{SUPERCHAT_API_URL}/api/v1/internal/telegram/incoming",
        content=body,
        headers=headers,
    )
    if response.status_code >= 400:
        logger.warning(
            "SuperChat rejected incoming message for %s: %s",
            user_id,
            response.status_code,
        )


def _require_enabled() -> None:
    if registry.disabled_reason is not None:
        raise HTTPException(status_code=503, detail=f"sidecar_disabled:{registry.disabled_reason}")


def _require_state(user_id: UUID) -> SessionState:
    state = registry.get(user_id)
    if state is None:
        raise HTTPException(status_code=404, detail="session_not_started")
    return state


def _build_display_name(entity: object) -> str:
    """Best-effort human-readable name for a Telegram user / channel / group."""
    if entity is None:
        return "Unknown"
    first = getattr(entity, "first_name", None)
    last = getattr(entity, "last_name", None)
    if first or last:
        return " ".join(part for part in (first, last) if part).strip() or "Unknown"
    title = getattr(entity, "title", None)
    if title:
        return title
    username = getattr(entity, "username", None)
    if username:
        return f"@{username}"
    return "Unknown"


def _build_chat_title(chat: object, fallback: str) -> str:
    """Title shown to the user for the source chat. For 1:1 dialogues with a
    private user Telegram does not expose a `title`; we fall back to the
    sender's display name so the UI never shows a raw chat id."""
    if chat is None:
        return fallback
    title = getattr(chat, "title", None)
    if title:
        return title
    username = getattr(chat, "username", None)
    if username:
        return f"@{username}"
    return fallback
