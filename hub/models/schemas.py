"""
Pydantic schemas for API request/response validation.
"""

from datetime import datetime
from typing import Optional, List
from pydantic import BaseModel, Field


# ─── Request Schemas ───────────────────────────────────────────────

class HeartbeatRequest(BaseModel):
    mac_address: str = Field(..., max_length=17, pattern=r"^([0-9A-Fa-f]{2}:){5}[0-9A-Fa-f]{2}$")
    battery_voltage: float = Field(..., ge=0.0, le=5.0)


# ─── Response Schemas ──────────────────────────────────────────────

class ChangeResult(BaseModel):
    changed: bool
    change_score: float = Field(..., ge=0.0, le=1.0)
    message: str = ""


class ImageUploadResponse(BaseModel):
    snapshot_id: int
    camera_mac: str
    captured_at: datetime
    change_result: ChangeResult


class AlertDto(BaseModel):
    id: int
    type: str
    severity: str
    message: str
    created_at: datetime
    is_read: bool

    class Config:
        from_attributes = True


class InventoryItemDto(BaseModel):
    id: int
    name: str
    category: Optional[str] = None
    quantity: int
    status: str
    last_seen: datetime
    days_missing: Optional[int] = None

    class Config:
        from_attributes = True


class ShelfInventoryDto(BaseModel):
    shelf_id: int
    shelf_name: str
    items: List[InventoryItemDto] = []


class InventoryResponse(BaseModel):
    shelves: List[ShelfInventoryDto] = []
    recent_alerts: List[AlertDto] = []


class CameraDto(BaseModel):
    id: int
    mac_address: str
    name: str
    shelf_id: int
    is_online: bool
    battery_voltage: float
    last_heartbeat: Optional[datetime] = None

    class Config:
        from_attributes = True


class HealthResponse(BaseModel):
    status: str = "ok"
    version: str = "0.1.0"
    cameras_online: int = 0
    cameras_total: int = 0
