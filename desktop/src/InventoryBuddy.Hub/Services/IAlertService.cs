using InventoryBuddy.Shared.Models;

namespace InventoryBuddy.Hub.Services;

/// <summary>
/// Generates and manages inventory alerts.
/// </summary>
public interface IAlertService
{
    /// <summary>Create and persist a new alert.</summary>
    Task<Alert> CreateAlert(AlertType type, AlertSeverity severity, string message,
        int? inventoryItemId = null, int? snapshotId = null);

    /// <summary>Mark stale Removed items as Depleted and fire alerts.</summary>
    Task<int> CheckStaleItems();

    /// <summary>Return recent unread alerts, optionally scoped to a shelf.</summary>
    Task<List<Alert>> GetRecentAlerts(int? shelfId = null, int take = 50);
}
