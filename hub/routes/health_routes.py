"""Health check and system status routes."""

from fastapi import APIRouter, Depends
from sqlalchemy.orm import Session

from hub.models.database import get_db, Camera
from hub.models.schemas import HealthResponse

router = APIRouter(prefix="/api", tags=["health"])


@router.get("/health", response_model=HealthResponse)
async def health(db: Session = Depends(get_db)):
    """System health check."""
    cameras = db.query(Camera).all()
    online = sum(1 for c in cameras if c.is_online)

    return HealthResponse(
        status="ok",
        version="0.1.0",
        cameras_online=online,
        cameras_total=len(cameras),
    )
