FROM mautrix-telegram-base:v0.21.0-py312

ENV VIRTUAL_ENV=/opt/telegram-helper-venv
ENV PATH="${VIRTUAL_ENV}/bin:${PATH}"

RUN python3 -m venv --system-site-packages "${VIRTUAL_ENV}" \
    && pip install --no-cache-dir aiohttp psycopg[binary] pyyaml

COPY telegram_room_helper.py /usr/local/bin/telegram_room_helper.py

CMD ["python", "/usr/local/bin/telegram_room_helper.py"]
