from pathlib import Path


TARGET = Path("/usr/lib/python3.12/site-packages/mautrix/appservice/api/intent.py")
OLD = '            await self.get_state_event(room_id, EventType.ROOM_CREATE, format="event")\n'
NEW = (
    "            # Synapse may omit event_id on state endpoints even with format=event.\n"
    "            # Content mode is enough here because StoreUpdatingAPI synthesizes a cached event.\n"
    "            await self.get_state_event(room_id, EventType.ROOM_CREATE)\n"
)


def main() -> None:
    if not TARGET.exists():
        print(f"Skipping intent.py patch: {TARGET} not found")
        return

    text = TARGET.read_text()
    if OLD not in text:
        print("Skipping intent.py patch: target snippet not present in this version")
        return

    TARGET.write_text(text.replace(OLD, NEW, 1))
    print("Patched intent.py successfully")


if __name__ == "__main__":
    main()
