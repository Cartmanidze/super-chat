FROM dock.mau.dev/mautrix/telegram:v0.15.3@sha256:04066316717e224d26677550089740f65345d64ea4a78187f5d4e0e66d970f61

ENV VIRTUAL_ENV=/opt/telegram-helper-venv
ENV PATH="${VIRTUAL_ENV}/bin:${PATH}"

RUN python3 -m venv --system-site-packages "${VIRTUAL_ENV}" \
    && pip install --no-cache-dir aiohttp psycopg[binary] pyyaml

COPY telegram_room_helper.py /usr/local/bin/telegram_room_helper.py

CMD ["python", "/usr/local/bin/telegram_room_helper.py"]
