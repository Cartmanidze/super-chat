FROM dock.mau.dev/mautrix/telegram:v0.15.3@sha256:04066316717e224d26677550089740f65345d64ea4a78187f5d4e0e66d970f61

COPY patch_mautrix_intent.py /tmp/patch_mautrix_intent.py
COPY patch_mautrix_diagnostics.py /tmp/patch_mautrix_diagnostics.py
COPY patch_telethon_session_save.py /tmp/patch_telethon_session_save.py

RUN python3 /tmp/patch_mautrix_intent.py \
    && python3 /tmp/patch_mautrix_diagnostics.py \
    && python3 /tmp/patch_telethon_session_save.py \
    && rm /tmp/patch_mautrix_intent.py /tmp/patch_mautrix_diagnostics.py /tmp/patch_telethon_session_save.py
