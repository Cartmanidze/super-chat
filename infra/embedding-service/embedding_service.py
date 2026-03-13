from __future__ import annotations

import hashlib
import importlib
import logging
import math
import os
import re
from functools import lru_cache
from typing import Iterable

import numpy as np
from fastapi import FastAPI, HTTPException
from pydantic import BaseModel, Field


LOGGER = logging.getLogger("embedding-service")
logging.basicConfig(level=os.getenv("LOG_LEVEL", "INFO"))

PROVIDER = (os.getenv("EMBEDDING_PROVIDER", "mock").strip() or "mock").lower()
MODEL_NAME = os.getenv("EMBEDDING_MODEL_NAME", "BAAI/bge-m3").strip() or "BAAI/bge-m3"
DEVICE = os.getenv("EMBEDDING_DEVICE", "cpu").strip() or "cpu"
DENSE_VECTOR_SIZE = max(1, int(os.getenv("EMBEDDING_DENSE_VECTOR_SIZE", "1024")))
EMBEDDING_VERSION = os.getenv("EMBEDDING_VERSION", "bge-m3-v1").strip() or "bge-m3-v1"
TOKEN_PATTERN = re.compile(r"\w+", re.UNICODE)

app = FastAPI(title="super-chat-embedding-service", version="0.1.0")


class EmbedRequest(BaseModel):
    text: str = Field(min_length=1, max_length=20000)


class SparseVectorPayload(BaseModel):
    indices: list[int]
    values: list[float]


class EmbedResponse(BaseModel):
    dense_vector: list[float]
    sparse_vector: SparseVectorPayload
    provider: str
    model: str
    embedding_version: str


@app.get("/health")
def health() -> dict[str, object]:
    return {
        "status": "ok",
        "provider": PROVIDER,
        "model": MODEL_NAME,
        "dense_vector_size": DENSE_VECTOR_SIZE,
    }


@app.post("/embed", response_model=EmbedResponse)
def embed(request: EmbedRequest) -> EmbedResponse:
    text = request.text.strip()
    if not text:
        raise HTTPException(status_code=400, detail="Text must not be empty.")

    try:
        if PROVIDER == "mock":
            dense_vector = build_mock_dense_vector(text)
            sparse_indices, sparse_values = build_mock_sparse_vector(text)
            provider_name = "mock"
        elif PROVIDER == "bgem3":
            dense_vector, sparse_indices, sparse_values = embed_with_bgem3(text)
            provider_name = "bgem3"
        else:
            raise HTTPException(status_code=400, detail=f"Unsupported embedding provider: {PROVIDER}")
    except HTTPException:
        raise
    except Exception as exc:  # pragma: no cover - defensive server boundary
        LOGGER.exception("Embedding generation failed.")
        raise HTTPException(status_code=500, detail=str(exc)) from exc

    return EmbedResponse(
        dense_vector=dense_vector,
        sparse_vector=SparseVectorPayload(indices=sparse_indices, values=sparse_values),
        provider=provider_name,
        model=MODEL_NAME,
        embedding_version=EMBEDDING_VERSION,
    )


def build_mock_dense_vector(text: str) -> list[float]:
    seed = normalize_text(text).encode("utf-8")
    values: list[float] = []
    counter = 0

    while len(values) < DENSE_VECTOR_SIZE:
        digest = hashlib.sha256(seed + counter.to_bytes(4, "little", signed=False)).digest()
        counter += 1

        for offset in range(0, len(digest), 4):
            chunk = digest[offset : offset + 4]
            if len(chunk) < 4:
                continue

            raw_value = int.from_bytes(chunk, "little", signed=False)
            scaled_value = (raw_value / 0xFFFFFFFF) * 2.0 - 1.0
            values.append(float(scaled_value))

            if len(values) == DENSE_VECTOR_SIZE:
                break

    norm = math.sqrt(sum(value * value for value in values)) or 1.0
    return [float(value / norm) for value in values]


def build_mock_sparse_vector(text: str) -> tuple[list[int], list[float]]:
    tokens = TOKEN_PATTERN.findall(normalize_text(text))
    if not tokens:
        tokens = ["empty"]

    by_index: dict[int, float] = {}
    token_count = len(tokens)

    for token in tokens:
        index = stable_sparse_index(token)
        by_index[index] = by_index.get(index, 0.0) + 1.0 / token_count

    pairs = sorted(by_index.items(), key=lambda pair: pair[0])
    indices = [index for index, _ in pairs]
    values = [float(weight) for _, weight in pairs]
    return indices, values


def embed_with_bgem3(text: str) -> tuple[list[float], list[int], list[float]]:
    model = get_bgem3_model()
    encoded = model.encode(
        [text],
        batch_size=1,
        max_length=8192,
        return_dense=True,
        return_sparse=True,
        return_colbert_vecs=False,
    )

    dense_vectors = encoded.get("dense_vecs") or encoded.get("dense_embeddings")
    if not dense_vectors:
        raise RuntimeError("BGE-M3 returned no dense vectors.")

    dense_vector = normalize_dense_vector(dense_vectors[0])
    lexical_weights = encoded.get("lexical_weights") or encoded.get("sparse_embeddings")
    sparse_vector = lexical_weights[0] if lexical_weights else {}
    sparse_indices, sparse_values = convert_sparse_weights(sparse_vector)

    return dense_vector, sparse_indices, sparse_values


def normalize_dense_vector(vector: Iterable[float]) -> list[float]:
    array = np.asarray(list(vector), dtype=np.float32)
    if array.size == 0:
        raise RuntimeError("Dense vector is empty.")

    norm = float(np.linalg.norm(array))
    if norm > 0:
        array = array / norm

    return array.astype(float).tolist()


def convert_sparse_weights(raw_weights: object) -> tuple[list[int], list[float]]:
    if raw_weights is None:
        return [], []

    if not isinstance(raw_weights, dict):
        raise RuntimeError("Sparse lexical weights must be a dictionary.")

    pairs: list[tuple[int, float]] = []
    for raw_key, raw_value in raw_weights.items():
        if raw_value is None:
            continue

        index = lexical_key_to_index(raw_key)
        value = float(raw_value)
        if value <= 0:
            continue

        pairs.append((index, value))

    pairs.sort(key=lambda pair: pair[0])
    indices = [index for index, _ in pairs]
    values = [value for _, value in pairs]
    return indices, values


def lexical_key_to_index(raw_key: object) -> int:
    if isinstance(raw_key, int):
        return raw_key

    if isinstance(raw_key, str):
        try:
            return int(raw_key)
        except ValueError:
            return stable_sparse_index(raw_key)

    return stable_sparse_index(str(raw_key))


def stable_sparse_index(text: str) -> int:
    digest = hashlib.sha1(text.encode("utf-8")).digest()
    return int.from_bytes(digest[:4], "little", signed=False)


def normalize_text(text: str) -> str:
    return " ".join(text.lower().split())


@lru_cache(maxsize=1)
def get_bgem3_model():
    try:
        flag_embedding = importlib.import_module("FlagEmbedding")
    except ImportError as exc:  # pragma: no cover - depends on optional build arg
        raise RuntimeError(
            "FlagEmbedding is not installed. Rebuild the image with EMBEDDING_INSTALL_BGE=1."
        ) from exc

    model_factory = getattr(flag_embedding, "BGEM3FlagModel", None)
    if model_factory is None:  # pragma: no cover - defensive
        raise RuntimeError("FlagEmbedding.BGEM3FlagModel is unavailable.")

    kwargs = {
        "model_name_or_path": MODEL_NAME,
        "use_fp16": DEVICE != "cpu",
    }

    try:
        return model_factory(device=DEVICE, **kwargs)
    except TypeError:
        return model_factory(**kwargs)
