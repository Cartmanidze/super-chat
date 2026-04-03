#!/bin/sh
set -eu

cat > /data/telegram-registration.yaml <<'EOF'
id: telegram
url: http://mautrix-telegram:29317
as_token: dev-mautrix-as-token
hs_token: dev-mautrix-hs-token
sender_localpart: telegrambot
rate_limited: false
namespaces:
    users:
        - regex: ^@telegrambot:matrix\.localhost$
          exclusive: true
        - regex: ^@telegram_.*:matrix\.localhost$
          exclusive: true
de.sorunome.msc2409.push_ephemeral: true
receive_ephemeral: true
EOF

chmod 644 /data/telegram-registration.yaml

exec /start.py
