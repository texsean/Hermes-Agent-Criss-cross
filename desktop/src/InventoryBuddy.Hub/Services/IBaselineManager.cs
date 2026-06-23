using InventoryBuddy.Shared.Models;

namespace InventoryBuddy.Hub.Services;

/// <summary>
/// Manages baseline (reference) images per camera.
/// </summary>
public interface IBaselineManager
{
    /// <summary>Persist a new baseline snapshot.</summary>
    Task<Snapshot> SaveBaseline(int cameraId, string filePath);

    /// <summary>Return the file path of the most recent baseline, or null.</summary>
    Task<string?> GetBaselinePath(int cameraId);

    /// <summary>Replace the current baseline. Creates one if none exists.</summary>
    Task<Snapshot> UpdateBaseline(int cameraId, string newFilePath);
}
