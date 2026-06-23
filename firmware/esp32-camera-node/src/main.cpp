/**
 * Inventory Buddy — ESP32-S3 Camera Node Firmware
 * ================================================
 * 
 * Lifecycle:
 *   1. Wake from deep sleep (PIR trigger or timer)
 *   2. Initialize camera
 *   3. Flash LED for consistent illumination
 *   4. Capture JPEG image (~30-80KB)
 *   5. Connect to Hub WiFi soft-AP
 *   6. HTTP POST image to Hub API
 *   7. Send ESP-NOW heartbeat
 *   8. Return to deep sleep
 *
 * ESP-NOW heartbeat runs on a separate task every 60 seconds
 * while the device is awake (before deep sleep).
 */

#include <Arduino.h>
#include <WiFi.h>
#include <HTTPClient.h>
#include <esp_now.h>
#include <esp_sleep.h>
#include <esp_camera.h>

#include "config.h"

// ─── Global State ─────────────────────────────────────────────────

static bool motion_detected = false;
static unsigned long last_heartbeat = 0;
static float battery_voltage = 0.0;

// ─── Camera Initialization ────────────────────────────────────────

bool init_camera() {
    camera_config_t config;
    config.ledc_channel = LEDC_CHANNEL_0;
    config.ledc_timer = LEDC_TIMER_0;
    config.pin_d0 = PIN_CAMERA_Y2;
    config.pin_d1 = PIN_CAMERA_Y3;
    config.pin_d2 = PIN_CAMERA_Y4;
    config.pin_d3 = PIN_CAMERA_Y5;
    config.pin_d4 = PIN_CAMERA_Y6;
    config.pin_d5 = PIN_CAMERA_Y7;
    config.pin_d6 = PIN_CAMERA_Y8;
    config.pin_d7 = PIN_CAMERA_Y9;
    config.pin_xclk = PIN_CAMERA_XCLK;
    config.pin_pclk = PIN_CAMERA_PCLK;
    config.pin_vsync = PIN_CAMERA_VSYNC;
    config.pin_href = PIN_CAMERA_HREF;
    config.pin_sccb_sda = PIN_CAMERA_SIOD;
    config.pin_sccb_scl = PIN_CAMERA_SIOC;
    config.pin_pwdn = PIN_CAMERA_PWDN;
    config.pin_reset = PIN_CAMERA_RESET;
    config.xclk_freq_hz = 20000000;
    config.pixel_format = PIXFORMAT_JPEG;
    config.frame_size = CAMERA_FRAME_SIZE;
    config.jpeg_quality = CAMERA_JPEG_QUALITY;
    config.fb_count = 1;

    esp_err_t err = esp_camera_init(&config);
    if (err != ESP_OK) {
        Serial.printf("Camera init failed: 0x%x\n", err);
        return false;
    }
    Serial.println("Camera initialized");
    return true;
}

// ─── WiFi Connection ──────────────────────────────────────────────

bool connect_wifi(unsigned long timeout_ms = 10000) {
    Serial.printf("Connecting to WiFi: %s\n", WIFI_SSID);
    WiFi.begin(WIFI_SSID, WIFI_PASSWORD);

    unsigned long start = millis();
    while (WiFi.status() != WL_CONNECTED && (millis() - start) < timeout_ms) {
        delay(500);
        Serial.print(".");
    }

    if (WiFi.status() == WL_CONNECTED) {
        Serial.printf("\nConnected! IP: %s\n", WiFi.localIP().toString().c_str());
        return true;
    }

    Serial.println("\nWiFi connection failed");
    return false;
}

// ─── Image Capture ────────────────────────────────────────────────

camera_fb_t* capture_image() {
    // Turn on LED flash
    pinMode(PIN_LED_FLASH, OUTPUT);
    digitalWrite(PIN_LED_FLASH, HIGH);
    delay(100);  // Let LED stabilize

    camera_fb_t* fb = esp_camera_fb_get();
    if (!fb) {
        Serial.println("Camera capture failed");
        digitalWrite(PIN_LED_FLASH, LOW);
        return nullptr;
    }

    digitalWrite(PIN_LED_FLASH, LOW);
    Serial.printf("Captured: %zu bytes\n", fb->len);
    return fb;
}

// ─── Upload to Hub ────────────────────────────────────────────────

bool upload_image(camera_fb_t* fb) {
    HTTPClient http;
    String url = "http://" + String(HUB_IP) + ":" + String(HUB_PORT) + 
                 "/api/camera/" + String(CAMERA_MAC) + "/image";

    http.begin(url);
    http.addHeader("Content-Type", "image/jpeg");
    
    int httpCode = http.POST(fb->buf, fb->len);
    
    if (httpCode == 200) {
        Serial.printf("Upload success: %d\n", httpCode);
        String response = http.getString();
        Serial.println(response);
        http.end();
        return true;
    }

    Serial.printf("Upload failed: %d\n", httpCode);
    http.end();
    return false;
}

// ─── Battery Reading ──────────────────────────────────────────────

float read_battery() {
    // ESP32-S3 ADC: read voltage divider (e.g., 100k + 100k for 18650 4.2V max)
    // Adjust based on your actual voltage divider!
    int raw = analogRead(1);  // ADC1_CH1 on GPIO1 (check your board)
    float voltage = (raw / 4095.0) * 3.3 * 2.0;  // 2:1 divider
    return voltage;
}

// ─── ESP-NOW Heartbeat ────────────────────────────────────────────

void send_espnow_heartbeat() {
    // For now, heartbeat goes via HTTP since ESP-NOW needs paired devices.
    // ESP-NOW broadcast requires hub to be in same ESP-NOW channel.
    // HTTP heartbeat is simpler and sufficient for Sprint 1.
    
    HTTPClient http;
    String url = "http://" + String(HUB_IP) + ":" + String(HUB_PORT) + "/api/camera/heartbeat";

    http.begin(url);
    http.addHeader("Content-Type", "application/json");
    
    String payload = "{\"mac_address\":\"" + String(CAMERA_MAC) + 
                     "\",\"battery_voltage\":" + String(battery_voltage, 2) + "}";
    
    int httpCode = http.POST(payload);
    
    if (httpCode == 200) {
        Serial.printf("Heartbeat sent: %.2fV\n", battery_voltage);
    } else {
        Serial.printf("Heartbeat failed: %d\n", httpCode);
    }
    
    http.end();
}

// ─── Deep Sleep ───────────────────────────────────────────────────

void enter_deep_sleep() {
    Serial.println("Entering deep sleep...");
    
    // Configure wake-up source: PIR on GPIO
    esp_sleep_enable_ext0_wakeup((gpio_num_t)PIN_PIR, HIGH);
    
    #if DEEP_SLEEP_SECONDS > 0
        esp_sleep_enable_timer_wakeup((uint64_t)DEEP_SLEEP_SECONDS * 1000000ULL);
    #endif
    
    delay(100);
    esp_deep_sleep_start();
}

// ─── Setup ────────────────────────────────────────────────────────

void setup() {
    Serial.begin(115200);
    delay(1000);
    
    Serial.println("\n=== Inventory Buddy Camera Node ===");
    Serial.printf("MAC: %s\n", CAMERA_MAC);
    Serial.printf("Name: %s\n", CAMERA_NAME);

    // Check wake reason
    esp_sleep_wakeup_cause_t wake_reason = esp_sleep_get_wakeup_cause();

    if (wake_reason == ESP_SLEEP_WAKEUP_EXT0) {
        Serial.println("Wake reason: PIR motion detected");
        motion_detected = true;
    } else if (wake_reason == ESP_SLEEP_WAKEUP_TIMER) {
        Serial.println("Wake reason: Timer (periodic check)");
    } else {
        Serial.println("Wake reason: Power-on reset");
    }

    // Read battery voltage
    battery_voltage = read_battery();
    Serial.printf("Battery: %.2fV\n", battery_voltage);

    // Initialize camera
    if (!init_camera()) {
        Serial.println("Camera init failed — sleeping");
        enter_deep_sleep();
        return;
    }

    // Connect to Hub WiFi
    if (!connect_wifi()) {
        Serial.println("WiFi failed — sleeping");
        enter_deep_sleep();
        return;
    }

    // Capture and upload image
    camera_fb_t* fb = capture_image();
    if (fb) {
        upload_image(fb);
        esp_camera_fb_return(fb);
    }

    // Send heartbeat
    send_espnow_heartbeat();

    // Go to sleep
    WiFi.disconnect(true);
    enter_deep_sleep();
}

// ─── Loop ─────────────────────────────────────────────────────────

void loop() {
    // Not used — ESP goes to deep sleep in setup().
    // If DEEP_SLEEP_SECONDS is 0 (debug mode), we stay awake for serial monitor.
    delay(10000);
}
