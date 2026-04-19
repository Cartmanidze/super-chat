"""Placeholder FastAPI service for the future Max userbot sidecar.

Mirrors the Telegram sidecar's endpoint shape so the .NET client can target the same
contract once the Max protocol implementation lands. For now every session endpoint
returns 501 Not Implemented.
"""
from __future__ import annotations

import logging
import os
from uuid import UUID

from fastapi import FastAPI, HTTPException
from pydantic import BaseModel

LOG_LEVEL = os.environ.get("LOG_LEVEL", "INFO")
logging.basicConfig(level=LOG_LEVEL, format="%(asctime)s %(levelname)s %(name)s %(message)s")
logger = logging.getLogger("max_userbot_service")

app = FastAPI(title="SuperChat Max Userbot (skeleton)")


class StartConnectRequest(BaseModel):
    phone: str


class SubmitCodeRequest(BaseModel):
    code: str


class SubmitPasswordRequest(BaseModel):
    password: str


@app.get("/health")
async def health() -> dict[str, str]:
    return {"status": "ok", "mode": "skeleton"}


@app.post("/sessions/{user_id}/connect")
async def connect(user_id: UUID, payload: StartConnectRequest) -> dict[str, str]:
    raise HTTPException(status_code=501, detail="max_protocol_not_implemented")


@app.post("/sessions/{user_id}/code")
async def submit_code(user_id: UUID, payload: SubmitCodeRequest) -> dict[str, str]:
    raise HTTPException(status_code=501, detail="max_protocol_not_implemented")


@app.post("/sessions/{user_id}/password")
async def submit_password(user_id: UUID, payload: SubmitPasswordRequest) -> dict[str, str]:
    raise HTTPException(status_code=501, detail="max_protocol_not_implemented")


@app.post("/sessions/{user_id}/disconnect")
async def disconnect(user_id: UUID) -> dict[str, str]:
    raise HTTPException(status_code=501, detail="max_protocol_not_implemented")


@app.get("/sessions/{user_id}/status")
async def status(user_id: UUID) -> dict[str, str]:
    return {"status": "not_started"}
