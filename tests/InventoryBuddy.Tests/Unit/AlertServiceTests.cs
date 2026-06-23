using FluentAssertions;
using InventoryBuddy.Hub.Data;
using InventoryBuddy.Hub.Services;
using InventoryBuddy.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace InventoryBuddy.Tests.Unit;

public class AlertServiceTests : IDisposable
{
    private readonly InventoryDbContext _db;
    private readonly AlertService _service;

    public AlertServiceTests()
    {
        var options = new DbContextOptionsBuilder<InventoryDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _db = new InventoryDbContext(options);
        _service = new AlertService(_db);
    }

    public void Dispose()
    {
        _db.Dispose();
    }

    // ── Test 1: CreateAlert persists to DB
    [Fact]
    public async Task CreateAlert_PersistsToDatabase()
    {
        // Arrange - need an inventory item first
        var item = new InventoryItem
        {
            ShelfId = 1,
            Name = "Test Item",
            Status = ItemStatus.Present
        };
        _db.InventoryItems.Add(item);
        await _db.SaveChangesAsync();

        // Act
        var alert = await _service.CreateAlert(
            inventoryItemId: item.Id,
            snapshotId: null,
            type: AlertType.ItemRemoved,
            severity: AlertSeverity.Warning,
            message: "Item was removed from shelf"
        );

        // Assert
        alert.Should().NotBeNull();
        alert.Id.Should().BeGreaterThan(0);
        alert.Type.Should().Be(AlertType.ItemRemoved);
        alert.Severity.Should().Be(AlertSeverity.Warning);
        alert.Message.Should().Be("Item was removed from shelf");
        alert.InventoryItemId.Should().Be(item.Id);

        // Verify in DB
        var fromDb = await _db.Alerts.FindAsync(alert.Id);
        fromDb.Should().NotBeNull();
        fromDb!.IsRead.Should().BeFalse();
    }

    // ── Test 2: CheckStaleItems marks items as Depleted when missing > 3 days
    [Fact]
    public async Task CheckStaleItems_MarksOldRemovedItemsAsDepleted()
    {
        // Arrange
        var staleItem = new InventoryItem
        {
            ShelfId = 1,
            Name = "Stale Item",
            Status = ItemStatus.Removed,
            RemovedAt = DateTime.UtcNow.AddDays(-5)
        };
        var recentItem = new InventoryItem
        {
            ShelfId = 1,
            Name = "Recently Removed",
            Status = ItemStatus.Removed,
            RemovedAt = DateTime.UtcNow.AddDays(-1)
        };
        var presentItem = new InventoryItem
        {
            ShelfId = 1,
            Name = "Still Present",
            Status = ItemStatus.Present
        };

        _db.InventoryItems.AddRange(staleItem, recentItem, presentItem);
        await _db.SaveChangesAsync();

        // Act
        int count = await _service.CheckStaleItems(staleDays: 3);

        // Assert
        count.Should().Be(1);

        // Reload from DB
        await _db.Entry(staleItem).ReloadAsync();
        await _db.Entry(recentItem).ReloadAsync();
        await _db.Entry(presentItem).ReloadAsync();

        staleItem.Status.Should().Be(ItemStatus.Depleted);
        recentItem.Status.Should().Be(ItemStatus.Removed);  // Not old enough
        presentItem.Status.Should().Be(ItemStatus.Present);   // Not removed at all
    }

    // ── Test 3: CheckStaleItems does NOT mark recently removed items
    [Fact]
    public async Task CheckStaleItems_DoesNotMarkRecentlyRemovedItems()
    {
        // Arrange
        var recentItem = new InventoryItem
        {
            ShelfId = 1,
            Name = "Recent Item",
            Status = ItemStatus.Removed,
            RemovedAt = DateTime.UtcNow.AddHours(-1)
        };
        _db.InventoryItems.Add(recentItem);
        await _db.SaveChangesAsync();

        // Act
        int count = await _service.CheckStaleItems(staleDays: 3);

        // Assert
        count.Should().Be(0);

        await _db.Entry(recentItem).ReloadAsync();
        recentItem.Status.Should().Be(ItemStatus.Removed);
    }

    // ── Test 4: CreateAlert with all parameters
    [Fact]
    public async Task CreateAlert_WithSnapshot_AssociatesCorrectly()
    {
        // Arrange
        var item = new InventoryItem { ShelfId = 1, Name = "Item", Status = ItemStatus.Present };
        var snapshot = new Snapshot { CameraId = 1, FilePath = "/test.jpg", Type = SnapshotType.Regular };
        _db.InventoryItems.Add(item);
        _db.Snapshots.Add(snapshot);
        await _db.SaveChangesAsync();

        // Act
        var alert = await _service.CreateAlert(
            inventoryItemId: item.Id,
            snapshotId: snapshot.Id,
            type: AlertType.ChangeDetected,
            severity: AlertSeverity.Info,
            message: "Change detected on shelf"
        );

        // Assert
        alert.SnapshotId.Should().Be(snapshot.Id);

        var fromDb = await _db.Alerts
            .Include(a => a.Snapshot)
            .FirstOrDefaultAsync(a => a.Id == alert.Id);

        fromDb!.Snapshot.Should().NotBeNull();
    }
}
