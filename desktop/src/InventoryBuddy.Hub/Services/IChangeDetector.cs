namespace InventoryBuddy.Hub.Services;

/// <summary>
/// Compares two shelf images to detect inventory changes.
/// </summary>
public interface IChangeDetector
{
    /// <summary>Compare baseline and current image by file path.</summary>
    ChangeResult Compare(string baselinePath, string currentPath);
}
