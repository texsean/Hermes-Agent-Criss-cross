# Inventory Buddy

**AI-powered shelf monitoring system.** Know what you have. Never run out.

---

## Architecture

```
┌──────────────────────────────────────────────────────┐
│                   YOUR SHELVES                        │
│                                                       │
│  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐   │
│  │ ESP32-S3 #1 │  │ ESP32-S3 #2 │  │ ESP32-S3 #3 │   │
│  │ Camera ▼    │  │ Camera ▼    │  │ Camera ▼    │   │
│  │ PIR trigger │  │ PIR trigger │  │ PIR trigger │   │
│  │ LED flash   │  │ LED flash   │  │ LED flash   │   │
│  │ 18650 bat.  │  │ 18650 bat.  │  │ 18650 bat.  │   │
│  └──────┬──────┘  └──────┬──────┘  └──────┬──────┘   │
│         │ WiFi            │                │          │
│         └─────────────────┼────────────────┘          │
│                           │                           │
│                    ┌──────┴──────┐                    │
│                    │ Raspberry   │                    │
│                    │ Pi 5 (Hub)  │                    │
│                    │ Python      │                    │
│                    │ FastAPI     │                    │
│                    │ OpenCV      │                    │
│                    │ SQLite      │                    │
│                    └──────┬──────┘                    │
└───────────────────────────┼───────────────────────────┘
                            │ WiFi / Ethernet
            ┌───────────────┼───────────────┐
            │               │               │
      ┌─────┴─────┐  ┌──────┴──────┐  ┌─────┴─────┐
      │ Windows   │  │  Android    │  │   iOS     │
      │ Desktop   │  │  (future)   │  │  (future) │
      │ C# / WPF  │  │  Kotlin     │  │  Swift    │
      └───────────┘  └─────────────┘  └───────────┘
```

## Project Structure

```
InventoryBuddy/
├── hub/                          # Python Hub — runs on Raspberry Pi 5
│   ├── main.py                   # FastAPI entry point
│   ├── requirements.txt          # Python dependencies
│   ├── routes/
│   │   ├── camera_routes.py      # Image ingestion + heartbeat
│   │   ├── inventory_routes.py   # Inventory queries
│   │   └── health_routes.py      # System health
│   ├── services/
│   │   ├── change_detector.py    # OpenCV pixel differencing
│   │   └── alert_engine.py       # Alert generation
│   ├── models/
│   │   ├── database.py           # SQLAlchemy models
│   │   └── schemas.py            # Pydantic schemas
│   └── firmware_bridge/
│       └── espnow_listener.py    # ESP-NOW relay listener
├── desktop/                      # C# Windows Desktop App (WPF)
│   └── src/
│       ├── InventoryBuddy.Hub/   # WPF application (was .Hub, rename pending)
│       └── InventoryBuddy.Shared/# Shared models/DTOs
├── mobile/
│   ├── android/README.md         # Android app placeholder
│   └── ios/README.md             # iOS app placeholder
├── firmware/
│   └── esp32-camera-node/        # PlatformIO project
│       ├── platformio.ini
│       ├── include/config.h
│       └── src/main.cpp
└── docs/
    └── sprint-1-plan.md
```

## How It Works

1. **Camera nodes** (ESP32-S3) wake on PIR motion, capture a JPEG, flash an LED for illumination, then upload the image to the Hub via WiFi.
2. **Hub** (Raspberry Pi 5) receives the image, saves it, compares against the baseline using OpenCV pixel differencing, and generates alerts if items have changed.
3. **Desktop/Mobile apps** query the Hub's REST API to display current inventory, alerts, and camera status.

## Sprint 1 — "Hello, Shelf"

**Goal:** One camera captures → Hub receives → detects change → desktop app displays result.

See `docs/sprint-1-plan.md` for full details.

## Setup (Raspberry Pi 5)

```bash
# Clone repo
git clone <repo-url>
cd InventoryBuddy/hub

# Install dependencies
pip install -r requirements.txt

# Run the Hub
python -m hub.main
# Or: uvicorn hub.main:app --host 0.0.0.0 --port 8000

# Data stored in /data/inventorybuddy/ (configurable via INVENTORY_BUDDY_DATA env var)
```

## Setup (Windows Desktop)

```bash
cd desktop
dotnet restore
dotnet build
dotnet run --project src/InventoryBuddy.Hub
```

## Setup (ESP32 Camera)

1. Install PlatformIO
2. Edit `firmware/esp32-camera-node/include/config.h` with your WiFi and Hub IP
3. Build and flash: `pio run -t upload`

---

**Privacy-first:** Cameras point downward at shelf contents only. Physically cannot capture faces or room views.
