"""
Routes for image ingestion and heartbeat from ESP32 camera nodes.
"""

import os
import uuid
import logging
from datetime import datetime, timezone
from fastapi import APIRouter, UploadFile, File, Form, Depends, HTTPException
from sqlalchemy.orm import Session

from hub.models.database import (
    get_db, Camera, Snapshot, SnapshotType, IMAGE_DIR,
)
from hub.models.schemas import ImageUploadResponse, ChangeResult, HeartbeatRequest
from hub.services.change_detector import ChangeDetector

logger = logging.getLogger("inventory_buddy.camera")
router = APIRouter(prefix="/api/camera", tags=["camera"])
change_detector = ChangeDetector()


@router.post("/{mac_address}/image", response_model=ImageUploadResponse)
async def upload_image(
    mac_address: str,
    image: UploadFile = File(...),
    captured_at: str = Form(None),
    db: Session = Depends(get_db),
):
    """
    Receive a JPEG image from an ESP32 camera node.
    Compares against baseline and returns change detection result.
    """
    # Validate MAC format
    mac_address = mac_address.lower().strip()

    # Find camera
    camera = db.query(Camera).filter(Camera.mac_address == mac_address).first()
    if not camera:
        raise HTTPException(status_code=404, detail=f"Camera {mac_address} not registered")

    # Validate content type
    if image.content_type not in ("image/jpeg", "image/png"):
        raise HTTPException(status_code=400, detail="Only JPEG and PNG images accepted")

    # Read image bytes
    image_bytes = await image.read()
    if len(image_bytes) == 0:
        raise HTTPException(status_code=400, detail="Empty image")

    # Save to disk
    timestamp = datetime.now(timezone.utc).strftime("%Y%m%d_%H%M%S_%f")
    ext = "jpg" if image.content_type == "image/jpeg" else "png"
    filename = f"cam_{camera.id}_{timestamp}.{ext}"
    filepath = os.path.join(IMAGE_DIR, filename)

    with open(filepath, "wb") as f:
        f.write(image_bytes)

    logger.info(f"Saved image from camera {mac_address}: {filepath} ({len(image_bytes)} bytes)")

    # Find or create baseline
    baseline_snapshot = (
        db.query(Snapshot)
        .filter(Snapshot.camera_id == camera.id, Snapshot.type == SnapshotType.BASELINE.value)
        .order_by(Snapshot.captured_at.desc())
        .first()
    )

    change_result = ChangeResult(changed=False, change_score=0.0, message="")

    if baseline_snapshot is None:
        # First image — set as baseline
        snapshot = Snapshot(
            camera_id=camera.id,
            file_path=filepath,
            type=SnapshotType.BASELINE.value,
            change_detected=False,
            change_score=0.0,
        )
        db.add(snapshot)
        db.commit()
        db.refresh(snapshot)

        change_result.message = "First image captured — set as baseline. No comparison yet."
        logger.info(f"Set baseline for camera {mac_address}")

        return ImageUploadResponse(
            snapshot_id=snapshot.id,
            camera_mac=mac_address,
            captured_at=snapshot.captured_at,
            change_result=change_result,
        )

    # Compare against baseline
    baseline_path = baseline_snapshot.file_path
    if not os.path.exists(baseline_path):
        logger.warning(f"Baseline file missing: {baseline_path}. Creating new baseline.")
        # Baseline file gone — treat current image as new baseline
        baseline_snapshot.type = SnapshotType.REGULAR.value  # demote old baseline
        snapshot = Snapshot(
            camera_id=camera.id,
            file_path=filepath,
            type=SnapshotType.BASELINE.value,
            change_detected=False,
            change_score=0.0,
        )
        db.add(snapshot)
        db.commit()
        db.refresh(snapshot)
        change_result.message = "Previous baseline lost — new baseline created."
        return ImageUploadResponse(
            snapshot_id=snapshot.id,
            camera_mac=mac_address,
            captured_at=snapshot.captured_at,
            change_result=change_result,
        )

    # Run change detection
    diff_result = change_detector.compare(baseline_path, filepath)

    # Save snapshot
    snapshot = Snapshot(
        camera_id=camera.id,
        file_path=filepath,
        type=SnapshotType.REGULAR.value,
        change_detected=diff_result["changed"],
        change_score=diff_result["change_score"],
        change_regions_json=str(diff_result.get("regions", [])),
    )
    db.add(snapshot)
    db.commit()
    db.refresh(snapshot)

    change_result = ChangeResult(
        changed=diff_result["changed"],
        change_score=diff_result["change_score"],
        message=f"Change detected: {diff_result['change_score']:.1%} of pixels differ"
        if diff_result["changed"]
        else "No significant change detected",
    )

    if diff_result["changed"]:
        logger.info(
            f"Change detected on camera {mac_address}: "
            f"score={diff_result['change_score']:.3f}, "
            f"regions={len(diff_result.get('regions', []))}"
        )

    return ImageUploadResponse(
        snapshot_id=snapshot.id,
        camera_mac=mac_address,
        captured_at=snapshot.captured_at,
        change_result=change_result,
    )


@router.post("/heartbeat")
async def heartbeat(
    body: HeartbeatRequest,
    db: Session = Depends(get_db),
):
    """Receive heartbeat from camera node (ESP-NOW or WiFi)."""
    camera = db.query(Camera).filter(Camera.mac_address == body.mac_address).first()
    if not camera:
        raise HTTPException(status_code=404, detail=f"Camera {body.mac_address} not registered")

    camera.last_heartbeat = datetime.now(timezone.utc)
    camera.battery_voltage = body.battery_voltage
    camera.is_online = True
    db.commit()

    return {"status": "ok", "mac_address": body.mac_address}
