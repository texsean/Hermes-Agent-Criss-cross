namespace InventoryBuddy.Shared.Models;

/// <summary>
/// A captured image from a camera at a point in time.
/// </summary>
public class Snapshot
{
    public int Id { get; set; }
    public int CameraId { get; set; }
    public DateTime CapturedAt { get; set; } = DateTime.UtcNow;
    public string FilePath { get; set; } = string.Empty;         // Path on Hub filesystem
    public SnapshotType Type { get; set; } = SnapshotType.Regular;
    public bool ChangeDetected { get; set; }
    public float ChangeScore { get; set; }                       // 0.0 - 1.0 diff magnitude
    public string? ChangeRegionsJson { get; set; }               // JSON: regions that changed

    // Navigation
    public Camera Camera { get; set; } = null!;
}

public enum SnapshotType
{
    Baseline,       // First image — the reference point
    Regular         // Ongoing capture for comparison
}
