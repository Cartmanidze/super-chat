FROM python:3.12-slim

ARG INSTALL_BGE=0

ENV PYTHONDONTWRITEBYTECODE=1 \
    PYTHONUNBUFFERED=1 \
    PIP_NO_CACHE_DIR=1 \
    EMBEDDING_PORT=7291

WORKDIR /app

COPY requirements.txt ./

RUN pip install --no-cache-dir -r requirements.txt \
    && if [ "$INSTALL_BGE" = "1" ]; then pip install --no-cache-dir "FlagEmbedding>=1.3.3,<2"; fi

COPY embedding_service.py ./

EXPOSE 7291

CMD ["sh", "-c", "uvicorn embedding_service:app --host 0.0.0.0 --port ${EMBEDDING_PORT:-7291}"]
