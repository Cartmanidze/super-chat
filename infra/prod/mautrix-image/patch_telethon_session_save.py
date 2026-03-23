"""Patch Telethon's TelegramBaseClient.__init__ to handle async session.save().

mautrix-telegram uses PgSession whose save() is an async coroutine, but
Telethon 1.99 calls session.save() synchronously inside __init__.  This
causes 'coroutine PgSession.save was never awaited' and the session is
silently lost on every restart, forcing a full MTProto handshake each time.

The fix: wrap the bare save() call so that when the return value is a
coroutine, it is scheduled on the running event loop.
"""

from pathlib import Path


TARGET = Path("/usr/lib/python3.12/site-packages/telethon/client/telegrambaseclient.py")

OLD = "            session.save()\n"
NEW = (
    "            _save_result = session.save()\n"
    "            if _save_result is not None:\n"
    "                import asyncio as _asyncio\n"
    "                try:\n"
    "                    _loop = _asyncio.get_running_loop()\n"
    "                    _loop.create_task(_save_result)\n"
    "                except RuntimeError:\n"
    "                    pass\n"
)


def main() -> None:
    text = TARGET.read_text()
    if OLD not in text:
        raise SystemExit("Target snippet not found in telethon telegrambaseclient.py")

    text = text.replace(OLD, NEW, 1)
    TARGET.write_text(text)


if __name__ == "__main__":
    main()
