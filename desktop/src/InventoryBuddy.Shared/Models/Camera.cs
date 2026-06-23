namespace InventoryBuddy.Shared.Models;

/// <summary>
/// An ESP32-S3 camera node mounted on a shelf.
/// </summary>
public class Camera
{
    public int Id { get; set; }
    public string MacAddress { get; set; } = string.Empty;     // ESP-NOW MAC address
    public string Name { get; set; } = string.Empty;            // e.g. "Shelf 1 Camera"
    public int ShelfId { get; set; }
    public DateTime LastHeartbeat { get; set; }
    public float BatteryVoltage { get; set; }
    public bool IsOnline { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public Shelf Shelf { get; set; } = null!;
    public List<Snapshot> Snapshots { get; set; } = new();
}
