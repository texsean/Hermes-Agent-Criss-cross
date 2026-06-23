using System.Drawing;

namespace InventoryBuddy.Hub.Services;

/// <summary>
/// Result of a change-detection comparison between two images.
/// </summary>
public class ChangeResult
{
    public bool Changed { get; set; }
    public float ChangeScore { get; set; }           // 0.0 – 1.0
    public List<Rectangle> ChangedRegions { get; set; } = new();
}
