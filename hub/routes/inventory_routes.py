"""
Routes for inventory queries — used by desktop and mobile apps.
"""

import logging
from datetime import datetime, timezone
from fastapi import APIRouter, Depends, HTTPException
from sqlalchemy.orm import Session

from hub.models.database import (
    get_db, Shelf, Camera, InventoryItem, Alert, Snapshot
)
from hub.models.schemas import (
    InventoryResponse, ShelfInventoryDto, InventoryItemDto, AlertDto, CameraDto
)

logger = logging.getLogger("inventory_buddy.inventory")
router = APIRouter(prefix="/api/inventory", tags=["inventory"])


@router.get("", response_model=InventoryResponse)
async def get_inventory(shelf_id: int = None, db: Session = Depends(get_db)):
    """
    Get inventory items, optionally filtered by shelf.
    Returns items grouped by shelf, plus recent alerts.
    """
    query = db.query(Shelf)
    if shelf_id is not None:
        query = query.filter(Shelf.id == shelf_id)

    shelves = query.all()
    if not shelves:
        return InventoryResponse(shelves=[], recent_alerts=[])

    now = datetime.now(timezone.utc)
    shelf_dtos = []

    for shelf in shelves:
        item_dtos = []
        for item in shelf.items:
            days_missing = None
            if item.removed_at:
                days_missing = (now - item.removed_at).days

            item_dtos.append(
                InventoryItemDto(
                    id=item.id,
                    name=item.name,
                    category=item.category,
                    quantity=item.quantity,
                    status=item.status,
                    last_seen=item.last_seen,
                    days_missing=days_missing,
                )
            )

        shelf_dtos.append(
            ShelfInventoryDto(
                shelf_id=shelf.id,
                shelf_name=shelf.name,
                items=item_dtos,
            )
        )

    # Recent alerts (last 50, newest first)
    recent_alerts = (
        db.query(Alert)
        .order_by(Alert.created_at.desc())
        .limit(50)
        .all()
    )

    alert_dtos = [
        AlertDto(
            id=a.id,
            type=a.type,
            severity=a.severity,
            message=a.message,
            created_at=a.created_at,
            is_read=a.is_read,
        )
        for a in recent_alerts
    ]

    return InventoryResponse(shelves=shelf_dtos, recent_alerts=alert_dtos)


@router.get("/cameras", response_model=list[CameraDto])
async def get_cameras(db: Session = Depends(get_db)):
    """List all registered cameras and their status."""
    cameras = db.query(Camera).all()
    return [
        CameraDto(
            id=c.id,
            mac_address=c.mac_address,
            name=c.name,
            shelf_id=c.shelf_id,
            is_online=c.is_online,
            battery_voltage=c.battery_voltage,
            last_heartbeat=c.last_heartbeat,
        )
        for c in cameras
    ]


@router.get("/alerts", response_model=list[AlertDto])
async def get_alerts(unread_only: bool = False, db: Session = Depends(get_db)):
    """Get alerts, optionally filtered to unread only."""
    query = db.query(Alert).order_by(Alert.created_at.desc()).limit(100)
    if unread_only:
        query = query.filter(Alert.is_read == False)

    alerts = query.all()
    return [
        AlertDto(
            id=a.id,
            type=a.type,
            severity=a.severity,
            message=a.message,
            created_at=a.created_at,
            is_read=a.is_read,
        )
        for a in alerts
    ]


@router.post("/alerts/{alert_id}/read")
async def mark_alert_read(alert_id: int, db: Session = Depends(get_db)):
    """Mark an alert as read."""
    alert = db.query(Alert).filter(Alert.id == alert_id).first()
    if not alert:
        raise HTTPException(status_code=404, detail="Alert not found")
    alert.is_read = True
    db.commit()
    return {"status": "ok"}
