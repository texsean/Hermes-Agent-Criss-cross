"""
Alert Engine
Background service that periodically checks for conditions requiring alerts:
- Items missing for > N days
- Cameras offline for > N minutes
- Items below minimum quantity
"""

import logging
from datetime import datetime, timezone, timedelta
from sqlalchemy.orm import Session

from hub.models.database import (
    SessionLocal, InventoryItem, Camera, Alert, ItemStatus, AlertType, AlertSeverity
)

logger = logging.getLogger("inventory_buddy.alert_engine")


class AlertEngine:
    """
    Checks system state and generates alerts.

    Config:
        stale_item_days: 3 — days since removal to mark "depleted"
        camera_offline_minutes: 60 — minutes without heartbeat to mark "offline"
    """

    def __init__(
        self,
        stale_item_days: int = 3,
        camera_offline_minutes: int = 60,
    ):
        self.stale_item_days = stale_item_days
        self.camera_offline_minutes = camera_offline_minutes

    def check_all(self):
        """Run all alert checks."""
        db = SessionLocal()
        try:
            self._check_stale_items(db)
            self._check_offline_cameras(db)
            self._check_low_stock(db)
            db.commit()
        except Exception as e:
            db.rollback()
            logger.error(f"Alert engine check failed: {e}", exc_info=True)
        finally:
            db.close()

    def _check_stale_items(self, db: Session):
        """
        Find items that were removed > stale_item_days ago and haven't returned.
        Mark as DEPLETED and create an alert.
        """
        cutoff = datetime.now(timezone.utc) - timedelta(days=self.stale_item_days)

        stale_items = (
            db.query(InventoryItem)
            .filter(
                InventoryItem.status == ItemStatus.REMOVED.value,
                InventoryItem.removed_at <= cutoff,
            )
            .all()
        )

        for item in stale_items:
            item.status = ItemStatus.DEPLETED.value

            # Create alert
            days = (datetime.now(timezone.utc) - item.removed_at).days
            alert = Alert(
                inventory_item_id=item.id,
                type=AlertType.ITEM_MISSING.value,
                severity=AlertSeverity.WARNING.value,
                message=f"{item.name} has been missing for {days} days — needs restock",
            )
            db.add(alert)

            logger.info(f"Stale item alert: {item.name} missing {days} days")

    def _check_offline_cameras(self, db: Session):
        """Mark cameras as offline if no heartbeat recently."""
        cutoff = datetime.now(timezone.utc) - timedelta(minutes=self.camera_offline_minutes)

        offline_cameras = (
            db.query(Camera)
            .filter(
                Camera.is_online == True,
                Camera.last_heartbeat <= cutoff,
            )
            .all()
        )

        for camera in offline_cameras:
            camera.is_online = False

            alert = Alert(
                type=AlertType.CAMERA_OFFLINE.value,
                severity=AlertSeverity.WARNING.value,
                message=f"Camera {camera.mac_address} ({camera.name}) is offline",
            )
            db.add(alert)

            logger.warning(f"Camera offline: {camera.mac_address} ({camera.name})")

    def _check_low_stock(self, db: Session):
        """Check items below minimum quantity."""
        low_items = (
            db.query(InventoryItem)
            .filter(
                InventoryItem.status == ItemStatus.PRESENT.value,
                InventoryItem.min_quantity.isnot(None),
                InventoryItem.quantity <= InventoryItem.min_quantity,
            )
            .all()
        )

        for item in low_items:
            # Only alert once per low-stock event (check if recent alert exists)
            existing_alert = (
                db.query(Alert)
                .filter(
                    Alert.inventory_item_id == item.id,
                    Alert.type == AlertType.LOW_STOCK.value,
                    Alert.resolved_at.is_(None),
                )
                .first()
            )

            if existing_alert:
                continue  # already alerted

            alert = Alert(
                inventory_item_id=item.id,
                type=AlertType.LOW_STOCK.value,
                severity=AlertSeverity.WARNING.value,
                message=f"Low stock: {item.name} has {item.quantity} (min: {item.min_quantity})",
            )
            db.add(alert)

            logger.info(f"Low stock alert: {item.name} qty={item.quantity}, min={item.min_quantity}")

    def create_alert(
        self,
        db: Session,
        alert_type: AlertType,
        severity: AlertSeverity,
        message: str,
        item_id: int = None,
        snapshot_id: int = None,
    ) -> Alert:
        """Create a generic alert."""
        alert = Alert(
            inventory_item_id=item_id,
            snapshot_id=snapshot_id,
            type=alert_type.value,
            severity=severity.value,
            message=message,
        )
        db.add(alert)
        db.flush()
        return alert
