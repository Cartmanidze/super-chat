FROM python:3.12-slim

ENV PYTHONDONTWRITEBYTECODE=1 \
    PYTHONUNBUFFERED=1 \
    PIP_NO_CACHE_DIR=1 \
    SERVICE_PORT=7491

WORKDIR /app

COPY requirements.txt ./
RUN pip install --no-cache-dir -r requirements.txt

COPY postgres_session.py telegram_userbot_service.py ./

EXPOSE 7491

CMD ["sh", "-c", "uvicorn telegram_userbot_service:app --host 0.0.0.0 --port ${SERVICE_PORT:-7491}"]
