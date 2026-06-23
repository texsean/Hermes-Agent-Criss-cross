using FluentAssertions;
using InventoryBuddy.Hub.Data;
using InventoryBuddy.Hub.Services;
using InventoryBuddy.Shared.Models;
using Microsoft.EntityFrameworkCore;
using SkiaSharp;

namespace InventoryBuddy.Tests.Unit;

public class BaselineManagerTests : IDisposable
{
    private readonly InventoryDbContext _db;
    private readonly BaselineManager _manager;

    public BaselineManagerTests()
    {
        var options = new DbContextOptionsBuilder<InventoryDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _db = new InventoryDbContext(options);
        _manager = new BaselineManager(_db);
    }

    public void Dispose()
    {
        _db.Dispose();
    }

    private static SKBitmap CreateTestImage()
    {
        var bmp = new SKBitmap(10, 10);
        bmp.Erase(SKColors.Blue);
        return bmp;
    }

    // ── Test 1: SaveBaseline creates a Snapshot with Type=Baseline
    [Fact]
    public async Task SaveBaseline_CreatesSnapshotWithBaselineType()
    {
        // Arrange
        using var image = CreateTestImage();
        const string filePath = "/images/baseline_1.jpg";

        // Act
        var snapshot = await _manager.SaveBaseline(cameraId: 1, image, filePath);

        // Assert
        snapshot.Should().NotBeNull();
        snapshot.CameraId.Should().Be(1);
        snapshot.Type.Should().Be(SnapshotType.Baseline);
        snapshot.FilePath.Should().Be(filePath);

        // Verify persisted in DB
        var fromDb = await _db.Snapshots.FindAsync(snapshot.Id);
        fromDb.Should().NotBeNull();
        fromDb!.Type.Should().Be(SnapshotType.Baseline);
    }

    // ── Test 2: GetBaseline returns the most recent baseline
    [Fact]
    public async Task GetBaseline_ReturnsMostRecentBaseline()
    {
        // Arrange
        using var image1 = CreateTestImage();
        using var image2 = CreateTestImage();

        var first = await _manager.SaveBaseline(1, image1, "/images/b1.jpg");
        await Task.Delay(10); // ensure different timestamps
        var second = await _manager.SaveBaseline(1, image2, "/images/b2.jpg");

        // Act
        var baseline = await _manager.GetBaseline(1);

        // Assert
        baseline.Should().NotBeNull();
        baseline!.Id.Should().Be(second.Id);
        baseline.CapturedAt.Should().BeAfter(first.CapturedAt);
    }

    // ── Test 3: UpdateBaseline replaces existing baseline
    [Fact]
    public async Task UpdateBaseline_ReplacesExistingBaseline()
    {
        // Arrange
        using var oldImage = CreateTestImage();
        await _manager.SaveBaseline(1, oldImage, "/images/old.jpg");

        // Act
        using var newImage = CreateTestImage();
        var updated = await _manager.UpdateBaseline(1, newImage, "/images/new.jpg");

        // Assert
        var baseline = await _manager.GetBaseline(1);
        baseline.Should().NotBeNull();
        baseline!.Id.Should().Be(updated.Id);
        baseline.FilePath.Should().Be("/images/new.jpg");

        // Old baseline should be demoted to Regular
        var allSnapshots = await _db.Snapshots.Where(s => s.CameraId == 1).ToListAsync();
        var regularCount = allSnapshots.Count(s => s.Type == SnapshotType.Regular);
        regularCount.Should().Be(1);

        var baselineCount = allSnapshots.Count(s => s.Type == SnapshotType.Baseline);
        baselineCount.Should().Be(1);
    }

    // ── Test 4: GetBaseline returns null when no baseline exists
    [Fact]
    public async Task GetBaseline_WhenNoBaselineExists_ReturnsNull()
    {
        // Act
        var baseline = await _manager.GetBaseline(cameraId: 99);

        // Assert
        baseline.Should().BeNull();
    }

    // ── Test 5: SaveBaseline for different cameras keeps separate baselines
    [Fact]
    public async Task SaveBaseline_DifferentCameras_HaveSeparateBaselines()
    {
        // Arrange
        using var img1 = CreateTestImage();
        using var img2 = CreateTestImage();

        // Act
        var cam1Baseline = await _manager.SaveBaseline(1, img1, "/images/cam1.jpg");
        var cam2Baseline = await _manager.SaveBaseline(2, img2, "/images/cam2.jpg");

        // Assert
        var b1 = await _manager.GetBaseline(1);
        var b2 = await _manager.GetBaseline(2);

        b1.Should().NotBeNull();
        b1!.Id.Should().Be(cam1Baseline.Id);
        b2.Should().NotBeNull();
        b2!.Id.Should().Be(cam2Baseline.Id);
    }
}
