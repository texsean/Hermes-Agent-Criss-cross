"""
ESP-NOW Bridge
Receives ESP-NOW packets from camera nodes on the Pi 5.
Uses a serial connection to an ESP32 co-processor or direct WiFi socket.

For Sprint 1: placeholder that listens on a UDP socket.
ESP32 nodes send small UDP packets with ESP-NOW-style metadata.
Future: dedicated ESP32 co-processor connected via UART.
"""

import asyncio
import json
import logging
import socket

logger = logging.getLogger("inventory_buddy.espnow_bridge")


class EspNowBridge:
    """
    Listens for ESP-NOW relay packets.
    
    Architecture:
        ESP32 cameras broadcast ESP-NOW packets → 
        A dedicated ESP32 receiver (connected to Pi 5 via UART/USB) relays them →
        This bridge receives them on a local socket.

    For Sprint 1 dev/testing: listens on UDP port 9999.
    Cameras send JSON: {"mac": "aa:bb:cc:dd:ee:ff", "battery": 3.7, "event": "heartbeat"}
    """

    def __init__(self, host: str = "0.0.0.0", port: int = 9999):
        self.host = host
        self.port = port
        self._running = False
        self._socket = None

    async def start(self):
        """Start listening for ESP-NOW relay packets."""
        self._socket = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
        self._socket.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
        self._socket.bind((self.host, self.port))
        self._socket.setblocking(False)
        self._running = True

        logger.info(f"ESP-NOW bridge listening on {self.host}:{self.port}")

        loop = asyncio.get_event_loop()

        while self._running:
            try:
                data, addr = await loop.sock_recvfrom(self._socket, 1024)
                await self._handle_packet(data, addr)
            except asyncio.CancelledError:
                break
            except Exception as e:
                logger.error(f"ESP-NOW bridge error: {e}")

    async def _handle_packet(self, data: bytes, addr: tuple):
        """Parse and forward ESP-NOW relayed packet to Hub API."""
        try:
            packet = json.loads(data.decode("utf-8"))
            mac = packet.get("mac", "unknown")
            battery = packet.get("battery", 0.0)
            event = packet.get("event", "heartbeat")

            logger.debug(f"ESP-NOW packet from {mac} ({addr}): event={event}, battery={battery}V")

            # Forward to Hub heartbeat endpoint
            import httpx
            async with httpx.AsyncClient() as client:
                await client.post(
                    "http://localhost:8000/api/camera/heartbeat",
                    json={"mac_address": mac, "battery_voltage": battery},
                    timeout=2.0,
                )

        except json.JSONDecodeError:
            logger.warning(f"Invalid JSON from {addr}: {data[:100]}")
        except Exception as e:
            logger.error(f"Failed to handle ESP-NOW packet from {addr}: {e}")

    async def stop(self):
        """Stop the bridge."""
        self._running = False
        if self._socket:
            self._socket.close()
            self._socket = None
        logger.info("ESP-NOW bridge stopped")


# Initialize as a module-level singleton
bridge = EspNowBridge()
