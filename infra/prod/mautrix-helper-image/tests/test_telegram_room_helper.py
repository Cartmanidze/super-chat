import asyncio
import importlib.util
import sys
import types
import unittest
from pathlib import Path
from unittest.mock import patch


def load_helper_module():
    web_module = types.SimpleNamespace(
        Request=type("Request", (), {}),
        Response=type("Response", (), {}),
        Application=type("Application", (), {}),
        json_response=lambda *args, **kwargs: None,
        run_app=lambda *args, **kwargs: None,
    )

    fake_modules = {
        "psycopg": types.SimpleNamespace(connect=lambda *args, **kwargs: None),
        "yaml": types.SimpleNamespace(safe_load=lambda text: {}),
        "aiohttp": types.SimpleNamespace(web=web_module),
        "telethon": types.SimpleNamespace(TelegramClient=type("TelegramClient", (), {}), types=types.SimpleNamespace()),
        "telethon.crypto": types.SimpleNamespace(AuthKey=lambda value: value),
        "telethon.sessions": types.SimpleNamespace(MemorySession=type("MemorySession", (), {})),
        "telethon.tl.functions.channels": types.SimpleNamespace(GetFullChannelRequest=type("GetFullChannelRequest", (), {})),
        "telethon.tl.functions.messages": types.SimpleNamespace(GetFullChatRequest=type("GetFullChatRequest", (), {})),
    }

    with patch.dict(sys.modules, fake_modules):
        spec = importlib.util.spec_from_file_location(
            "telegram_room_helper_under_test",
            Path(__file__).resolve().parents[1] / "telegram_room_helper.py",
        )
        module = importlib.util.module_from_spec(spec)
        assert spec.loader is not None
        spec.loader.exec_module(module)
        return module


class _FakeCursor:
    def __init__(self, portal_row):
        self._portal_row = portal_row

    def execute(self, query, params):
        self._last_query = query
        self._last_params = params

    def fetchone(self):
        return self._portal_row

    def __enter__(self):
        return self

    def __exit__(self, exc_type, exc, tb):
        return False


class _FakeConnection:
    def __init__(self, portal_row):
        self._portal_row = portal_row

    def cursor(self):
        return _FakeCursor(self._portal_row)

    def __enter__(self):
        return self

    def __exit__(self, exc_type, exc, tb):
        return False


class TelegramRoomHelperTests(unittest.TestCase):
    def test_fetch_room_info_returns_direct_room_without_session_lookup(self):
        module = load_helper_module()

        with patch.object(module.psycopg, "connect", return_value=_FakeConnection((123, "user", "Alex", False))):
            with patch.object(module, "get_session_row", side_effect=AssertionError("session lookup should not run")):
                payload = asyncio.run(
                    module.fetch_room_info(
                        "postgresql://ignored",
                        1000,
                        "hash",
                        "@pilot:matrix.localhost",
                        "!dm:matrix.localhost",
                    )
                )

        self.assertEqual(
            {
                "room_id": "!dm:matrix.localhost",
                "peer_type": "user",
                "participant_count": 2,
                "title": "Alex",
                "is_broadcast_channel": False,
            },
            payload,
        )


if __name__ == "__main__":
    unittest.main()
