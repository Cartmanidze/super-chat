FROM mautrix-telegram-base:v0.21.0-py312

COPY patch_mautrix_intent.py /tmp/patch_mautrix_intent.py
COPY patch_mautrix_diagnostics.py /tmp/patch_mautrix_diagnostics.py
COPY patch_telethon_session_save.py /tmp/patch_telethon_session_save.py

RUN python3 /tmp/patch_mautrix_intent.py \
    && python3 /tmp/patch_mautrix_diagnostics.py \
    && python3 /tmp/patch_telethon_session_save.py \
    && rm /tmp/patch_mautrix_intent.py /tmp/patch_mautrix_diagnostics.py /tmp/patch_telethon_session_save.py

CMD ["/opt/mautrix-telegram/docker-run.sh"]
