/**
 * Camera Node Configuration
 * Edit these values for your deployment.
 */

#ifndef CONFIG_H
#define CONFIG_H

// ─── WiFi Configuration ──────────────────────────────────────────
// The Hub (Pi 5) runs a WiFi soft-AP. Camera nodes connect to it.
#define WIFI_SSID           "InventoryBuddyHub"
#define WIFI_PASSWORD       "buddy2024"

// Hub IP address on soft-AP network (usually 192.168.4.1)
#define HUB_IP              "192.168.4.1"
#define HUB_PORT            8000

// ─── Camera Identity ─────────────────────────────────────────────
// Unique MAC-like ID for this camera node.
// Must match the MAC registered in the Hub database.
#define CAMERA_MAC          "aa:bb:cc:dd:ee:01"
#define CAMERA_NAME         "Shelf 1 Camera"

// ─── ESP-NOW ──────────────────────────────────────────────────────
// Broadcast MAC (all cameras broadcast to hub)
#define ESPNOW_BROADCAST    {0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF}

// ESP-NOW packet types
#define ESPNOW_TYPE_HEARTBEAT   0x01
#define ESPNOW_TYPE_MOTION      0x02
#define ESPNOW_TYPE_STATUS      0x03

// Heartbeat interval (milliseconds)
#define HEARTBEAT_INTERVAL_MS   60000   // 60 seconds

// ─── Camera Settings ─────────────────────────────────────────────
// OV2640 camera configuration
#define CAMERA_FRAME_SIZE   FRAMESIZE_VGA     // 640x480
#define CAMERA_JPEG_QUALITY  12               // 0-63, lower = better quality

// ─── Power Management ────────────────────────────────────────────
// Deep sleep duration after image capture (microseconds)
// 0 = don't sleep (debug mode)
#define DEEP_SLEEP_SECONDS  0     // Set to e.g. 300 for 5 min between checks

// ─── Pins (AI-Thinker ESP32-CAM pinout) ──────────────────────────
#define PIN_PIR             13    // PIR motion sensor (wake trigger)
#define PIN_LED_FLASH       4     // White LED for illumination
#define PIN_CAMERA_PWDN     -1    // Camera power-down (not used on AI-Thinker)
#define PIN_CAMERA_RESET    15
#define PIN_CAMERA_XCLK     27
#define PIN_CAMERA_SIOD     25
#define PIN_CAMERA_SIOC     23
#define PIN_CAMERA_Y9       19
#define PIN_CAMERA_Y8       36
#define PIN_CAMERA_Y7       18
#define PIN_CAMERA_Y6       39
#define PIN_CAMERA_Y5       5
#define PIN_CAMERA_Y4       34
#define PIN_CAMERA_Y3       35
#define PIN_CAMERA_Y2       32
#define PIN_CAMERA_VSYNC    22
#define PIN_CAMERA_HREF     26
#define PIN_CAMERA_PCLK     21

#endif // CONFIG_H
