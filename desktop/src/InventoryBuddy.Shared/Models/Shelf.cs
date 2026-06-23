namespace InventoryBuddy.Shared.Models;

/// <summary>
/// A physical shelf being monitored.
/// </summary>
public class Shelf
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;          // e.g. "Pantry Shelf 1"
    public string Location { get; set; } = string.Empty;      // e.g. "Kitchen Pantry"
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public List<Camera> Cameras { get; set; } = new();
    public List<InventoryItem> Items { get; set; } = new();
}
