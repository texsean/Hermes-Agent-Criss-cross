using Microsoft.EntityFrameworkCore;
using InventoryBuddy.Hub.Data;
using InventoryBuddy.Shared.Models;

namespace InventoryBuddy.Hub.Services;

/// <summary>
/// Manages baseline (reference) images per camera.
/// Provides the "before" image that ChangeDetector compares against.
/// </summary>
public class BaselineManager : IBaselineManager
{
    private readonly IServiceScopeFactory _scopeFactory;

    public BaselineManager(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    /// <inheritdoc />
    public async Task<Snapshot> SaveBaseline(int cameraId, string filePath)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();

        var snapshot = new Snapshot
        {
            CameraId = cameraId,
            FilePath = filePath,
            Type = SnapshotType.Baseline,
            CapturedAt = DateTime.UtcNow,
            ChangeDetected = false,
            ChangeScore = 0
        };

        db.Snapshots.Add(snapshot);
        await db.SaveChangesAsync();
        return snapshot;
    }

    /// <inheritdoc />
    public async Task<string?> GetBaselinePath(int cameraId)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();

        var baseline = await db.Snapshots
            .Where(s => s.CameraId == cameraId && s.Type == SnapshotType.Baseline)
            .OrderByDescending(s => s.CapturedAt)
            .FirstOrDefaultAsync();

        return baseline?.FilePath;
    }

    /// <inheritdoc />
    public async Task<Snapshot> UpdateBaseline(int cameraId, string newFilePath)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();

        // Remove all existing baselines for this camera
        var oldBaselines = await db.Snapshots
            .Where(s => s.CameraId == cameraId && s.Type == SnapshotType.Baseline)
            .ToListAsync();

        db.Snapshots.RemoveRange(oldBaselines);

        var snapshot = new Snapshot
        {
            CameraId = cameraId,
            FilePath = newFilePath,
            Type = SnapshotType.Baseline,
            CapturedAt = DateTime.UtcNow,
            ChangeDetected = false,
            ChangeScore = 0
        };

        db.Snapshots.Add(snapshot);
        await db.SaveChangesAsync();
        return snapshot;
    }
}
