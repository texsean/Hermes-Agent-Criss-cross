# Inventory Buddy — Android App (Placeholder)
# =============================================
# This directory is a placeholder for the future Android app.
# 
# Planned tech stack:
#   - Kotlin + Jetpack Compose
#   - Retrofit for REST API calls to Hub
#   - Room for local cache
#   - Firebase for push notifications
#   - MVVM architecture
#
# Key features (post-Sprint-1):
#   - View inventory by shelf
#   - Receive push alerts (item missing, low stock)
#   - Label unknown items (tap to name)
#   - Barcode scanning for item identification
#   - Camera offline notifications
#   - Setup wizard (pair cameras, configure shelves)
#
# API endpoints consumed:
#   GET  /api/inventory          — list all inventory
#   GET  /api/inventory?sid=1    — filter by shelf
#   GET  /api/inventory/cameras  — camera status
#   GET  /api/inventory/alerts   — recent alerts
#   POST /api/inventory/alerts/{id}/read — mark read
#   GET  /api/health             — system health
#
# Package name: com.inventorybuddy.android
# Min SDK: 26 (Android 8.0)
# Target SDK: 34 (Android 14)
