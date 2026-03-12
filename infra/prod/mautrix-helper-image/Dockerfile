FROM dock.mau.dev/mautrix/telegram@sha256:9378e650d699a974f29959e78dafca3b4eeab094887ccb82f261168c6b3bd835

ENV VIRTUAL_ENV=/opt/telegram-helper-venv
ENV PATH="${VIRTUAL_ENV}/bin:${PATH}"

RUN python3 -m venv "${VIRTUAL_ENV}" \
    && pip install --no-cache-dir aiohttp psycopg[binary] pyyaml

COPY telegram_room_helper.py /usr/local/bin/telegram_room_helper.py

CMD ["python", "/usr/local/bin/telegram_room_helper.py"]
