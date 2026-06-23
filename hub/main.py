"""
Inventory Buddy Hub — Main Entry Point
Runs on Raspberry Pi 5.

Responsibilities:
- REST API for desktop/mobile apps and camera nodes
- Image ingestion from ESP32 cameras
- OpenCV-based change detection (pixel differencing)
- SQLite inventory database
- Alert engine (missing items, low stock, camera offline)
- ESP-NOW bridge for camera heartbeat monitoring
"""

import logging
from contextlib import asynccontextmanager
from fastapi import FastAPI
from fastapi.middleware.cors import CORSMiddleware

from hub.models.database import init_db
from hub.routes import camera_routes, inventory_routes, health_routes
from hub.services.alert_engine import AlertEngine

logger = logging.getLogger("inventory_buddy")


@asynccontextmanager
async def lifespan(app: FastAPI):
    """Startup/shutdown lifecycle."""
    logger.info("Inventory Buddy Hub starting...")
    init_db()
    logger.info("Database initialized.")

    # Start alert engine background task
    # (runs periodically to check for stale items, offline cameras, etc.)
    # asyncio.create_task(AlertEngine.run_periodic())

    yield

    logger.info("Inventory Buddy Hub shutting down.")


app = FastAPI(
    title="Inventory Buddy Hub",
    version="0.1.0",
    description="Central controller for Inventory Buddy — AI-powered shelf monitoring",
    lifespan=lifespan,
)

# Allow desktop app (any origin during dev) and mobile apps
app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"],
    allow_methods=["*"],
    allow_headers=["*"],
)

# Register route modules
app.include_router(camera_routes.router)
app.include_router(inventory_routes.router)
app.include_router(health_routes.router)
