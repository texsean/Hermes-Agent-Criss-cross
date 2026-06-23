"""
SQLAlchemy models and SQLite database initialization for the Hub.
"""

import os
from datetime import datetime, timezone
from sqlalchemy import (
    create_engine, Column, Integer, String, Float, DateTime, ForeignKey, Boolean, Enum as SAEnum, Text
)
from sqlalchemy.orm import declarative_base, relationship, sessionmaker

DATA_DIR = os.environ.get("INVENTORY_BUDDY_DATA", "/data/inventorybuddy")
os.makedirs(DATA_DIR, exist_ok=True)

DB_PATH = os.path.join(DATA_DIR, "inventory.db")
IMAGE_DIR = os.path.join(DATA_DIR, "images")
os.makedirs(IMAGE_DIR, exist_ok=True)

engine = create_engine(f"sqlite:///{DB_PATH}", echo=False)
SessionLocal = sessionmaker(bind=engine, autocommit=False, autoflush=False)
Base = declarative_base()

import enum


class ItemStatus(str, enum.Enum):
    PRESENT = "present"
    REMOVED = "removed"
    DEPLETED = "depleted"
    NEW = "new"


class SnapshotType(str, enum.Enum):
    BASELINE = "baseline"
    REGULAR = "regular"


class AlertType(str, enum.Enum):
    ITEM_REMOVED = "item_removed"
    ITEM_ADDED = "item_added"
    LOW_STOCK = "low_stock"
    ITEM_MISSING = "item_missing"
    CAMERA_OFFLINE = "camera_offline"
    CHANGE_DETECTED = "change_detected"


class AlertSeverity(str, enum.Enum):
    INFO = "info"
    WARNING = "warning"
    CRITICAL = "critical"


class Shelf(Base):
    __tablename__ = "shelves"

    id = Column(Integer, primary_key=True, autoincrement=True)
    name = Column(String(100), nullable=False)
    location = Column(String(200), default="")
    created_at = Column(DateTime, default=lambda: datetime.now(timezone.utc))

    cameras = relationship("Camera", back_populates="shelf")
    items = relationship("InventoryItem", back_populates="shelf")


class Camera(Base):
    __tablename__ = "cameras"

    id = Column(Integer, primary_key=True, autoincrement=True)
    mac_address = Column(String(17), unique=True, nullable=False, index=True)
    name = Column(String(100), default="")
    shelf_id = Column(Integer, ForeignKey("shelves.id"), nullable=False)
    last_heartbeat = Column(DateTime, nullable=True)
    battery_voltage = Column(Float, default=0.0)
    is_online = Column(Boolean, default=False)
    created_at = Column(DateTime, default=lambda: datetime.now(timezone.utc))

    shelf = relationship("Shelf", back_populates="cameras")
    snapshots = relationship("Snapshot", back_populates="camera")


class InventoryItem(Base):
    __tablename__ = "inventory_items"

    id = Column(Integer, primary_key=True, autoincrement=True)
    shelf_id = Column(Integer, ForeignKey("shelves.id"), nullable=False)
    name = Column(String(200), nullable=False)
    category = Column(String(50), nullable=True)
    quantity = Column(Integer, default=1)
    min_quantity = Column(Integer, nullable=True)
    status = Column(String(20), default=ItemStatus.PRESENT.value)
    first_seen = Column(DateTime, default=lambda: datetime.now(timezone.utc))
    last_seen = Column(DateTime, default=lambda: datetime.now(timezone.utc))
    removed_at = Column(DateTime, nullable=True)
    image_region_bbox = Column(Text, nullable=True)  # JSON string

    shelf = relationship("Shelf", back_populates="items")
    alerts = relationship("Alert", back_populates="inventory_item")


class Snapshot(Base):
    __tablename__ = "snapshots"

    id = Column(Integer, primary_key=True, autoincrement=True)
    camera_id = Column(Integer, ForeignKey("cameras.id"), nullable=False)
    captured_at = Column(DateTime, default=lambda: datetime.now(timezone.utc))
    file_path = Column(String(500), nullable=False)
    type = Column(String(10), default=SnapshotType.REGULAR.value)
    change_detected = Column(Boolean, default=False)
    change_score = Column(Float, default=0.0)
    change_regions_json = Column(Text, nullable=True)

    camera = relationship("Camera", back_populates="snapshots")


class Alert(Base):
    __tablename__ = "alerts"

    id = Column(Integer, primary_key=True, autoincrement=True)
    inventory_item_id = Column(Integer, ForeignKey("inventory_items.id"), nullable=True)
    snapshot_id = Column(Integer, ForeignKey("snapshots.id"), nullable=True)
    type = Column(String(30), nullable=False)
    severity = Column(String(10), default=AlertSeverity.INFO.value)
    message = Column(String(500), nullable=False)
    is_read = Column(Boolean, default=False)
    created_at = Column(DateTime, default=lambda: datetime.now(timezone.utc))
    resolved_at = Column(DateTime, nullable=True)

    inventory_item = relationship("InventoryItem", back_populates="alerts")
    snapshot = relationship("Snapshot")


def init_db():
    """Create all tables if they don't exist."""
    Base.metadata.create_all(bind=engine)


def get_db():
    """Dependency: yields a DB session."""
    db = SessionLocal()
    try:
        yield db
    finally:
        db.close()
