namespace InventoryBuddy.Shared.Models;

/// <summary>
/// An alert generated when inventory changes meaningfully.
/// </summary>
public class Alert
{
    public int Id { get; set; }
    public int? InventoryItemId { get; set; }
    public int? SnapshotId { get; set; }
    public AlertType Type { get; set; }
    public AlertSeverity Severity { get; set; } = AlertSeverity.Info;
    public string Message { get; set; } = string.Empty;
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ResolvedAt { get; set; }

    // Navigation
    public InventoryItem? InventoryItem { get; set; }
    public Snapshot? Snapshot { get; set; }
}

public enum AlertType
{
    ItemRemoved,        // Something was taken
    ItemAdded,          // Something new appeared
    LowStock,           // Quantity below MinQuantity
    ItemMissing,        // Item gone for N days (stale alert)
    CameraOffline,      // No heartbeat from camera
    ChangeDetected      // Generic: something changed, needs user labeling
}

public enum AlertSeverity
{
    Info,
    Warning,
    Critical
}
