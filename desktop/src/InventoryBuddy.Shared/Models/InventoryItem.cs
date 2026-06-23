namespace InventoryBuddy.Shared.Models;

/// <summary>
/// An item tracked in inventory.
/// </summary>
public class InventoryItem
{
    public int Id { get; set; }
    public int ShelfId { get; set; }
    public string Name { get; set; } = string.Empty;            // User-labeled: "Baked Beans", "Socket 9mm"
    public string? Category { get; set; }                        // e.g. "Groceries", "Tools"
    public int Quantity { get; set; } = 1;
    public int? MinQuantity { get; set; }                        // Threshold for "needs restock" alert
    public ItemStatus Status { get; set; } = ItemStatus.Present;
    public DateTime FirstSeen { get; set; } = DateTime.UtcNow;
    public DateTime LastSeen { get; set; } = DateTime.UtcNow;
    public DateTime? RemovedAt { get; set; }                     // When it disappeared
    public string? ImageRegionBbox { get; set; }                 // JSON: bounding box coords in shelf image

    // Navigation
    public Shelf Shelf { get; set; } = null!;
    public List<Alert> Alerts { get; set; } = new();
}

public enum ItemStatus
{
    Present,        // Currently on shelf
    Removed,        // Was removed (may come back)
    Depleted,       // Gone for N days — needs restock
    New             // Just appeared, not yet labeled
}
