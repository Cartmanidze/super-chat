"""Custom Telethon Session backed by PostgreSQL with AES-GCM encryption of auth_key.

Stores only the essentials to resume a Telegram connection: dc_id, server_address, port,
and the encrypted auth_key. Entity/file caches live in memory and are rebuilt by Telethon
as needed when the client starts.

I/O строится на async psycopg, чтобы один пользователь, который сейчас пишет сессию
в БД, не блокировал event-loop для всех остальных. Telethon-сеттеры вызываются
синхронно (`session.auth_key = key`), поэтому реальная запись запускается через
fire-and-forget asyncio.create_task — память обновляется немедленно, БД догоняет
за миллисекунды.
"""
from __future__ import annotations

import asyncio
import base64
import logging
import os
import secrets
from dataclasses import dataclass
from typing import Optional
from uuid import UUID

import psycopg
from cryptography.hazmat.primitives.ciphers.aead import AESGCM
from telethon.crypto import AuthKey
from telethon.sessions import MemorySession

logger = logging.getLogger("telegram_userbot_service.postgres_session")


@dataclass
class SessionRow:
    user_id: UUID
    dc_id: int
    server_address: str
    port: int
    auth_key_encrypted: bytes


class SessionCipher:
    """Wraps AES-GCM encryption using a key supplied via environment variable.

    The key is expected to be base64-encoded 16/24/32 bytes. Nonce is generated
    per encryption and prepended to the ciphertext so decryption is self-contained.
    """

    NONCE_SIZE = 12

    def __init__(self, key_material: bytes) -> None:
        if len(key_material) not in (16, 24, 32):
            raise ValueError("Encryption key must be 16, 24, or 32 bytes.")
        self._aes = AESGCM(key_material)

    @classmethod
    def from_environment(cls, env_name: str = "TELEGRAM_SESSION_ENCRYPTION_KEY") -> "SessionCipher":
        raw = os.environ.get(env_name)
        if not raw:
            raise RuntimeError(f"Environment variable {env_name} is not set.")
        try:
            key_material = base64.b64decode(raw, validate=True)
        except ValueError as exc:
            raise RuntimeError(f"{env_name} must be base64-encoded.") from exc
        return cls(key_material)

    def encrypt(self, plaintext: bytes) -> bytes:
        nonce = secrets.token_bytes(self.NONCE_SIZE)
        ciphertext = self._aes.encrypt(nonce, plaintext, associated_data=None)
        return nonce + ciphertext

    def decrypt(self, payload: bytes) -> bytes:
        if len(payload) <= self.NONCE_SIZE:
            raise ValueError("Encrypted payload is too short.")
        nonce = payload[: self.NONCE_SIZE]
        ciphertext = payload[self.NONCE_SIZE :]
        return self._aes.decrypt(nonce, ciphertext, associated_data=None)


class PostgresSessionStore:
    """Async helper that reads/writes session rows through psycopg."""

    def __init__(self, dsn: str, cipher: SessionCipher) -> None:
        self._dsn = dsn
        self._cipher = cipher

    async def load(self, user_id: UUID) -> Optional[SessionRow]:
        async with await psycopg.AsyncConnection.connect(self._dsn) as connection:
            async with connection.cursor() as cursor:
                await cursor.execute(
                    """
                    select user_id, dc_id, server_address, port, auth_key_encrypted
                    from telegram_sessions
                    where user_id = %s
                    """,
                    (str(user_id),),
                )
                row = await cursor.fetchone()
                if row is None:
                    return None
                return SessionRow(
                    user_id=UUID(str(row[0])),
                    dc_id=row[1],
                    server_address=row[2],
                    port=row[3],
                    auth_key_encrypted=bytes(row[4]),
                )

    async def upsert(self, row: SessionRow) -> None:
        async with await psycopg.AsyncConnection.connect(self._dsn) as connection:
            async with connection.cursor() as cursor:
                await cursor.execute(
                    """
                    insert into telegram_sessions
                        (user_id, dc_id, server_address, port, auth_key_encrypted, created_at, updated_at)
                    values (%s, %s, %s, %s, %s, now(), now())
                    on conflict (user_id) do update set
                        dc_id = excluded.dc_id,
                        server_address = excluded.server_address,
                        port = excluded.port,
                        auth_key_encrypted = excluded.auth_key_encrypted,
                        updated_at = now()
                    """,
                    (str(row.user_id), row.dc_id, row.server_address, row.port, row.auth_key_encrypted),
                )
            await connection.commit()

    async def delete(self, user_id: UUID) -> None:
        async with await psycopg.AsyncConnection.connect(self._dsn) as connection:
            async with connection.cursor() as cursor:
                await cursor.execute(
                    "delete from telegram_sessions where user_id = %s",
                    (str(user_id),),
                )
            await connection.commit()

    async def list_user_ids(self) -> list[UUID]:
        """Возвращает все user_id с сохранёнными Telegram-сессиями.
        Используется на старте sidecar для auto-resume."""
        async with await psycopg.AsyncConnection.connect(self._dsn) as connection:
            async with connection.cursor() as cursor:
                await cursor.execute("select user_id from telegram_sessions")
                rows = await cursor.fetchall()
        return [UUID(str(row[0])) for row in rows]

    @property
    def cipher(self) -> SessionCipher:
        return self._cipher


class PostgresSession(MemorySession):
    """Telethon Session that persists auth_key and DC info to PostgreSQL.

    Relies on MemorySession for entity/file caches to keep the schema small.
    On every DC/auth_key change мы планируем upsert через asyncio.create_task,
    чтобы Telethon-сеттер вернулся мгновенно и не блокировал event-loop.

    Note on the `auth_key` override: MemorySession exposes auth_key through a
    property backed by `self._auth_key`. We redefine that property here with our
    own setter so every assignment (including Telethon's internal ones during
    sign_in) triggers persistence. The getter keeps reading `self._auth_key`
    from the base class state.
    """

    def __init__(self, user_id: UUID, store: PostgresSessionStore) -> None:
        super().__init__()
        self._user_id = user_id
        self._store = store
        # Храним сильные ссылки на background-таски записи в БД, иначе GC может
        # съесть их до завершения и часть upsert-ов потеряется.
        self._pending_persists: set[asyncio.Task] = set()

    @classmethod
    async def restore(cls, user_id: UUID, store: PostgresSessionStore) -> "PostgresSession":
        session = cls(user_id, store)
        row = await store.load(user_id)
        if row is None:
            return session

        # Пишем напрямую в state MemorySession, чтобы не вызвать redundant persist
        # обратно в БД на этапе восстановления.
        session._dc_id = row.dc_id
        session._server_address = row.server_address
        session._port = row.port
        try:
            key_bytes = store.cipher.decrypt(row.auth_key_encrypted)
            session._auth_key = AuthKey(key_bytes)
        except Exception:
            session._auth_key = None
        return session

    def set_dc(self, dc_id: int, server_address: str, port: int) -> None:
        super().set_dc(dc_id, server_address, port)
        self._schedule_persist()

    @property
    def auth_key(self):
        return self._auth_key

    @auth_key.setter
    def auth_key(self, value: Optional[AuthKey]) -> None:
        self._auth_key = value
        self._schedule_persist()

    def _schedule_persist(self) -> None:
        """Fire-and-forget запись в БД. Telethon-сеттеры синхронны, поэтому
        реальный upsert уезжает в asyncio.create_task; результат отслеживаем
        через done-callback, чтобы не потерять traceback при ошибке."""
        if self._auth_key is None or self._dc_id == 0:
            return

        try:
            loop = asyncio.get_running_loop()
        except RuntimeError:
            # Нет активного event-loop'а (например, на этапе теста/импорта) —
            # просто пропускаем; данные останутся только в памяти, что для
            # нон-продакшн-сценариев приемлемо.
            return

        task = loop.create_task(self._persist())
        self._pending_persists.add(task)
        task.add_done_callback(self._on_persist_done)

    def _on_persist_done(self, task: asyncio.Task) -> None:
        self._pending_persists.discard(task)
        if task.cancelled():
            return
        exc = task.exception()
        if exc is not None:
            logger.exception(
                "Failed to persist Telegram session for %s",
                self._user_id,
                exc_info=exc,
            )

    async def _persist(self) -> None:
        if self._auth_key is None or self._dc_id == 0:
            return

        encrypted = self._store.cipher.encrypt(self._auth_key.key)
        await self._store.upsert(
            SessionRow(
                user_id=self._user_id,
                dc_id=self._dc_id,
                server_address=self._server_address,
                port=self._port,
                auth_key_encrypted=encrypted,
            )
        )

    async def forget(self) -> None:
        await self._store.delete(self._user_id)
