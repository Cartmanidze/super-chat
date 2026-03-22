from pathlib import Path


USER_TARGET = Path("/usr/lib/python3.12/site-packages/mautrix_telegram/user.py")
PORTAL_TARGET = Path("/usr/lib/python3.12/site-packages/mautrix_telegram/portal.py")

USER_REPLACEMENTS = [
    (
        "        try:\n            await super().start()\n",
        "        try:\n"
        "            self.log.debug(\n"
        "                f\"Calling AbstractUser.start() for {self.name} \"\n"
        "                f\"(delete_unless_authenticated={delete_unless_authenticated})\"\n"
        "            )\n"
        "            await super().start()\n"
        "            self.log.debug(\n"
        "                f\"AbstractUser.start() returned for {self.name}; \"\n"
        "                f\"connected={self.client.is_connected() if self.client else False}\"\n"
        "            )\n",
    ),
    (
        "        except Exception:\n            await self.push_bridge_state(BridgeStateEvent.UNKNOWN_ERROR)\n            raise\n",
        "        except Exception:\n"
        "            self.log.exception(\n"
        "                f\"Unexpected exception before GetStateRequest() in start() for {self.name}\"\n"
        "            )\n"
        "            await self.push_bridge_state(BridgeStateEvent.UNKNOWN_ERROR)\n"
        "            raise\n",
    ),
    (
        "            await self.client(GetStateRequest())\n",
        "            self.log.debug(f\"Sending GetStateRequest() for {self.name}\")\n"
        "            await self.client(GetStateRequest())\n"
        "            self.log.debug(f\"GetStateRequest() completed for {self.name}\")\n",
    ),
]

PORTAL_IMPORT_OLD = "    ChatWriteForbiddenError,\n"
PORTAL_IMPORT_NEW = "    ChatWriteForbiddenError,\n    AuthKeyUnregisteredError,\n"

PORTAL_HANDLER_OLD = """    async def handle_telegram_message(\n        self, source: au.AbstractUser, sender: p.Puppet | None, evt: Message\n    ) -> None:\n        try:\n            await self._handle_telegram_message(source, sender, evt)\n        except Exception:\n            sender_id = sender.tgid if sender else None\n            self.log.exception(\n                f\"Failed to handle Telegram message {evt.id} from {sender_id} via {source.tgid}\"\n            )\n            if self.config[\"bridge.incoming_bridge_error_reports\"]:\n                intent = sender.intent_for(self) if sender else self.main_intent\n                await self._send_message(\n                    intent,\n                    TextMessageEventContent(\n                        msgtype=MessageType.NOTICE,\n                        body=\"Error processing message from Telegram\",\n                    ),\n                )\n"""

PORTAL_HANDLER_NEW = """    async def handle_telegram_message(\n        self, source: au.AbstractUser, sender: p.Puppet | None, evt: Message\n    ) -> None:\n        try:\n            await self._handle_telegram_message(source, sender, evt)\n        except AuthKeyUnregisteredError as e:\n            sender_id = sender.tgid if sender else None\n            self.log.exception(\n                f\"AuthKeyUnregisteredError while handling Telegram message {evt.id} \"\n                f\"from {sender_id} via {source.tgid}; forcing sign-out\"\n            )\n            if hasattr(source, \"on_signed_out\"):\n                await source.on_signed_out(e)\n        except Exception as e:\n            sender_id = sender.tgid if sender else None\n            self.log.exception(\n                f\"Failed to handle Telegram message {evt.id} from {sender_id} via \"\n                f\"{source.tgid} ({type(e).__name__})\"\n            )\n            if self.config[\"bridge.incoming_bridge_error_reports\"]:\n                intent = sender.intent_for(self) if sender else self.main_intent\n                await self._send_message(\n                    intent,\n                    TextMessageEventContent(\n                        msgtype=MessageType.NOTICE,\n                        body=\"Error processing message from Telegram\",\n                    ),\n                )\n"""


def replace_once(path: Path, old: str, new: str, label: str) -> None:
    text = path.read_text(encoding="utf-8")
    if old not in text:
        raise SystemExit(f"Target snippet not found in {path.name}: {label}")

    path.write_text(text.replace(old, new, 1), encoding="utf-8")


def main() -> None:
    for index, (old, new) in enumerate(USER_REPLACEMENTS, start=1):
        replace_once(USER_TARGET, old, new, f"user replacement #{index}")

    replace_once(PORTAL_TARGET, PORTAL_IMPORT_OLD, PORTAL_IMPORT_NEW, "portal import")
    replace_once(PORTAL_TARGET, PORTAL_HANDLER_OLD, PORTAL_HANDLER_NEW, "portal handler")


if __name__ == "__main__":
    main()
