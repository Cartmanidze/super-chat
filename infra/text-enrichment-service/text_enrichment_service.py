from __future__ import annotations

import importlib
import logging
import os
import re
from functools import lru_cache
from typing import Any

import uvicorn
from fastapi import FastAPI
from pydantic import BaseModel, Field


LOGGER = logging.getLogger("text-enrichment-service")
logging.basicConfig(level=os.getenv("TEXT_ENRICHMENT_LOG_LEVEL", "INFO"))

PORT = max(1, int(os.getenv("TEXT_ENRICHMENT_PORT", "7391")))
HEIDELTIME_LANGUAGE = os.getenv("TEXT_ENRICHMENT_HEIDELTIME_LANGUAGE", "Russian").strip() or "Russian"
HEIDELTIME_DOCUMENT_TYPE = os.getenv("TEXT_ENRICHMENT_HEIDELTIME_DOCUMENT_TYPE", "colloquial").strip() or "colloquial"

app = FastAPI(title="super-chat-text-enrichment-service", version="0.1.0")


class AnalyzeRequest(BaseModel):
    text: str = Field(min_length=1, max_length=20000)
    reference_time_utc: str
    time_zone_id: str


class EntityPayload(BaseModel):
    text: str
    type: str
    normalized_text: str | None = None


class TemporalExpressionPayload(BaseModel):
    text: str
    value: str | None = None
    grain: str | None = None


class AnalyzeResponse(BaseModel):
    counterparty_name: str | None = None
    organization_name: str | None = None
    entities: list[EntityPayload] = []
    temporal_expressions: list[TemporalExpressionPayload] = []


@app.get("/health")
def health() -> dict[str, object]:
    return {
        "status": "ok",
        "natasha": natasha_available(),
        "heideltime": heideltime_available(),
        "language": HEIDELTIME_LANGUAGE,
        "document_type": HEIDELTIME_DOCUMENT_TYPE,
    }


@app.post("/analyze", response_model=AnalyzeResponse)
def analyze(request: AnalyzeRequest) -> AnalyzeResponse:
    text = request.text.strip()
    entities = extract_entities(text)
    temporal_expressions = extract_temporal_expressions(text, request.reference_time_utc)

    counterparty_name = detect_counterparty_name(text, entities)
    organization_name = detect_organization_name(text, entities)

    return AnalyzeResponse(
        counterparty_name=counterparty_name,
        organization_name=organization_name,
        entities=entities,
        temporal_expressions=temporal_expressions,
    )


def extract_entities(text: str) -> list[EntityPayload]:
    runtime = get_natasha_runtime()
    if runtime is None:
        return []

    segmenter, morph_vocab, ner_tagger, doc_type = runtime
    doc = doc_type(text)
    doc.segment(segmenter)
    doc.tag_ner(ner_tagger)

    entities: list[EntityPayload] = []
    for span in doc.spans:
        normalized_text = None
        try:
            span.normalize(morph_vocab)
            normalized_text = getattr(span, "normal", None)
        except Exception:
            normalized_text = None

        entities.append(
            EntityPayload(
                text=span.text,
                type=span.type,
                normalized_text=normalized_text,
            )
        )

    return entities


def extract_temporals(text: str, reference_time_utc: str) -> list[TemporalExpressionPayload]:
    heideltime_fn = get_heideltime_function()
    if heideltime_fn is None:
        return []

    normalized_reference = reference_time_utc[:10]
    try:
        raw = call_heideltime(heideltime_fn, text, normalized_reference)
    except Exception as exc:  # pragma: no cover - optional dependency boundary
        LOGGER.warning("HeidelTime extraction failed: %s", exc)
        return []

    return normalize_temporal_payload(raw)


def detect_counterparty_name(text: str, entities: list[EntityPayload]) -> str | None:
    intro_patterns = [
        r"меня зовут\s+(?P<name>[А-ЯЁ][а-яё]+(?:\s+[А-ЯЁ][а-яё]+){0,2})",
        r"my name is\s+(?P<name>[A-Z][a-z]+(?:\s+[A-Z][a-z]+){0,2})",
        r"это\s+(?P<name>[А-ЯЁ][а-яё]+(?:\s+[А-ЯЁ][а-яё]+){0,2})",
    ]

    for pattern in intro_patterns:
        match = re.search(pattern, text, re.IGNORECASE)
        if match:
            return match.group("name").strip()

    person = next((entity for entity in entities if entity.type.upper() == "PER"), None)
    if person is not None:
        return (person.normalized_text or person.text).strip()

    return None


def detect_organization_name(text: str, entities: list[EntityPayload]) -> str | None:
    org = next((entity for entity in entities if entity.type.upper() == "ORG"), None)
    if org is not None:
        return (org.normalized_text or org.text).strip()

    match = re.search(
        r"(?:компан(?:ию|ия)|company)\s+(?P<org>[A-Za-zА-Яа-яЁё0-9][A-Za-zА-Яа-яЁё0-9\s\.\-&]{1,80})",
        text,
        re.IGNORECASE,
    )
    if match:
        return match.group("org").strip(" ,.")

    return None


def natasha_available() -> bool:
    return get_natasha_runtime() is not None


def heideltime_available() -> bool:
    return get_heideltime_function() is not None


@lru_cache(maxsize=1)
def get_natasha_runtime() -> tuple[Any, Any, Any, Any] | None:
    try:
        natasha = importlib.import_module("natasha")
    except ImportError:
        LOGGER.warning("Natasha is not installed.")
        return None

    return (
        natasha.Segmenter(),
        natasha.MorphVocab(),
        natasha.NewsNERTagger(natasha.NewsEmbedding()),
        natasha.Doc,
    )


@lru_cache(maxsize=1)
def get_heideltime_function():
    candidates = [
        ("py_heideltime", "heideltime"),
        ("py_heideltime", "py_heideltime"),
        ("heideltime", "heideltime"),
    ]

    for module_name, function_name in candidates:
        try:
            module = importlib.import_module(module_name)
        except ImportError:
            continue

        function = getattr(module, function_name, None)
        if function is not None:
            return function

    LOGGER.warning("HeidelTime wrapper is not installed.")
    return None


def call_heideltime(heideltime_fn, text: str, reference_date: str):
    attempts = [
        {"language": HEIDELTIME_LANGUAGE, "document_type": HEIDELTIME_DOCUMENT_TYPE, "dct": reference_date},
        {"language": HEIDELTIME_LANGUAGE, "document_type": HEIDELTIME_DOCUMENT_TYPE, "document_creation_time": reference_date},
    ]

    for kwargs in attempts:
        try:
            return heideltime_fn(text, **kwargs)
        except TypeError:
            continue

    return heideltime_fn(text)


def normalize_temporal_payload(raw: Any) -> list[TemporalExpressionPayload]:
    if raw is None:
        return []

    items: list[dict[str, Any]] = []
    if isinstance(raw, list):
        items = [item for item in raw if isinstance(item, dict)]
    elif isinstance(raw, dict):
        for key in ("temporal_expressions", "timexes", "timex", "results", "annotations"):
            candidate = raw.get(key)
            if isinstance(candidate, list):
                items = [item for item in candidate if isinstance(item, dict)]
                break

    payload: list[TemporalExpressionPayload] = []
    for item in items:
        text = str(item.get("text") or item.get("span") or item.get("value_text") or "").strip()
        if not text:
            continue

        value = item.get("value") or item.get("timexValue") or item.get("normalized")
        grain = item.get("grain") or item.get("type") or item.get("timexType")

        payload.append(
            TemporalExpressionPayload(
                text=text,
                value=str(value).strip() if value else None,
                grain=str(grain).strip() if grain else None,
            )
        )

    return payload


if __name__ == "__main__":
    uvicorn.run(app, host="0.0.0.0", port=PORT)
