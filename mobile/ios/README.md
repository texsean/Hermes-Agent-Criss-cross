# Inventory Buddy — iOS App (Placeholder)
# =========================================
# This directory is a placeholder for the future iOS app.
#
# Planned tech stack:
#   - Swift + SwiftUI
#   - URLSession / Alamofire for REST API
#   - Core Data for local cache
#   - APNs for push notifications
#   - MVVM architecture
#
# Key features (post-Sprint-1):
#   - View inventory by shelf
#   - Receive push alerts (item missing, low stock)
#   - Label unknown items (tap to name)
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
# Bundle ID: com.inventorybuddy.ios
# Min target: iOS 16.0
