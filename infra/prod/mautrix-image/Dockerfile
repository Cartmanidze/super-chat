FROM dock.mau.dev/mautrix/telegram@sha256:9378e650d699a974f29959e78dafca3b4eeab094887ccb82f261168c6b3bd835

COPY patch_mautrix_intent.py /tmp/patch_mautrix_intent.py
COPY patch_mautrix_history.py /tmp/patch_mautrix_history.py

RUN python3 /tmp/patch_mautrix_intent.py \
    && python3 /tmp/patch_mautrix_history.py \
    && rm /tmp/patch_mautrix_intent.py /tmp/patch_mautrix_history.py
