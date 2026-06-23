# Sprint 1: "Hello, Shelf" — Foundation & First Contact

**Duration:** 2 weeks  
**Goal:** One camera captures an image → Hub receives it → detects change from baseline → desktop app displays the result.

---

## User Stories

| ID | Story | Priority | Acceptance Criteria |
|----|-------|----------|---------------------|
| US-01 | As a developer, I can run the Python Hub on a Pi 5 and it responds to HTTP requests | P0 | GET /api/health returns 200 OK |
| US-02 | As a camera node, I capture a JPEG and upload it to the Hub via WiFi | P0 | Image lands on Hub disk; DB record created |
| US-03 | As the Hub, I compare a new image against the stored baseline and detect changes | P1 | Change score returned; >15% diff triggers "changed" flag |
| US-04 | As a user, I open the Windows Desktop app and see inventory + alerts from the Hub | P1 | WPF app displays inventory grid and alert list |
| US-05 | As a user, I am alerted when an item has been missing for 3+ days | P2 | Alert generated with item name and days missing |
| US-06 | As an operator, I see camera battery status and online/offline state | P2 | Heartbeat endpoint updates; offline cameras flagged after 60 min |

---

## Architecture (Sprint 1)

### Hardware
- **Raspberry Pi 5** — runs Python Hub (FastAPI + OpenCV + SQLite)
- **1× ESP32-S3 Camera Node** — OV2640 camera, PIR trigger, white LED, 18650 battery
- **Windows PC** — runs C# WPF desktop app

### Data Flow
```
PIR triggered → ESP32 wakes → LED flash → Capture JPEG →
  WiFi connect to Hub AP → POST /api/camera/{mac}/image →
  Hub saves JPEG → compares to baseline (OpenCV) →
  returns change score → Desktop app polls /api/inventory
```

---

## Tech Stack

| Component | Technology |
|-----------|-----------|
| Hub API | Python 3.11 + FastAPI |
| Database | SQLite + SQLAlchemy |
| Image processing | OpenCV (pixel differencing) |
| Desktop app | C# / .NET 8 / WPF |
| Camera firmware | C++ / Arduino / ESP-IDF |
| Camera protocol | WiFi (soft-AP) + ESP-NOW (heartbeat) |
| Mobile apps | Placeholder only (Android README, iOS README) |

---

## API Endpoints (Sprint 1)

| Method | Path | Description |
|--------|------|-------------|
| POST | /api/camera/{mac}/image | Upload JPEG from camera |
| POST | /api/camera/heartbeat | Camera battery/heartbeat update |
| GET | /api/inventory | List all inventory (optionally by shelf) |
| GET | /api/inventory/cameras | Camera status list |
| GET | /api/inventory/alerts | Recent alerts |
| POST | /api/inventory/alerts/{id}/read | Mark alert as read |
| GET | /api/health | System health |

---

## Database Schema

**shelves**: id, name, location, created_at  
**cameras**: id, mac_address (unique), name, shelf_id (FK), last_heartbeat, battery_voltage, is_online  
**inventory_items**: id, shelf_id (FK), name, category, quantity, min_quantity, status, first_seen, last_seen, removed_at, image_region_bbox  
**snapshots**: id, camera_id (FK), captured_at, file_path, type (baseline/regular), change_detected, change_score, change_regions_json  
**alerts**: id, inventory_item_id (FK), snapshot_id (FK), type, severity, message, is_read, created_at, resolved_at

---

## Change Detection Algorithm (Sprint 1)

1. Load baseline + current image via OpenCV
2. Resize both to 640×480
3. Apply Gaussian blur (5×5 kernel) to reduce sensor noise
4. Compute absolute pixel difference
5. Threshold at 30 (intensity difference per channel)
6. Count changed pixels → change_score = changed/total
7. Find connected components (changed regions with area ≥ 100px)
8. Return: { changed: bool, change_score: float, regions: [...] }

Config: min_changed_pixels=500 → about 0.15% of frame = "changed"

---

## Sprint 1 Deliverables Checklist

- [x] Project structure created
- [x] Python Hub: main.py, routes, services, models
- [x] C# Desktop: WPF app with API client
- [x] C# Shared: DTOs matching Hub API
- [x] ESP32 firmware: camera capture + WiFi upload + heartbeat
- [x] Mobile placeholders (Android README, iOS README)
- [ ] Unit tests for ChangeDetector
- [ ] Unit tests for AlertEngine
- [ ] Integration tests for Hub API
- [ ] Camera Simulator for testing without hardware
- [ ] First end-to-end test: CameraSim → Hub → Desktop app

---

## Out of Scope (Sprint 2+)

- AI object recognition / labeling (Sprint 2)
- Android app implementation (Sprint 3)
- iOS app implementation (Sprint 4)
- Barcode scanning
- Multi-camera simultaneous tracking
- Cloud sync / backup
- Fridge/condensation-resistant hardware variant
