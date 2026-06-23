using FluentAssertions;
using InventoryBuddy.Hub.Data;
using InventoryBuddy.Hub.Services;
using InventoryBuddy.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace InventoryBuddy.Tests.Unit;

public class BaselineManagerTests : IDisposable
{
    private readonly InventoryDbContext _db;
    private readonly BaselineManager _manager;

    public BaselineManagerTests()
    {
        // Create an InMemory database
        var options = new DbContextOptionsBuilder<InventoryDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _db = new InventoryDbContext(options);

        // BaselineManager uses IServiceScopeFactory (singleton pattern)
        // We create a scope factory that returns our InMemory DbContext
        var serviceProvider = new ServiceCollection()
            .AddSingleton(_db)
            .BuildServiceProvider();

        var scopeFactory = new TestScopeFactory(serviceProvider);
        _manager = new BaselineManager(scopeFactory);
    }

    public void Dispose()
    {
        _db.Dispose();
    }

    // Simple IServiceScopeFactory that wraps an existing ServiceProvider
    private class TestScopeFactory : IServiceScopeFactory
    {
        private readonly IServiceProvider _provider;
        public TestScopeFactory(IServiceProvider provider) => _provider = provider;
        public IServiceScope CreateScope() => new TestScope(_provider);
    }

    private class TestScope : IServiceScope
    {
        public IServiceProvider ServiceProvider { get; }
        public TestScope(IServiceProvider provider) => ServiceProvider = provider;
        public void Dispose() { }
    }

    // ── Test 1: SaveBaseline creates a Snapshot with Type=Baseline
    [Fact]
    public async Task SaveBaseline_CreatesSnapshotWithBaselineType()
    {
        // Arrange
        const string filePath = "/images/baseline_cam1.jpg";

        // Act
        var snapshot = await _manager.SaveBaseline(cameraId: 1, filePath);

        // Assert
        snapshot.Should().NotBeNull();
        snapshot.CameraId.Should().Be(1);
        snapshot.Type.Should().Be(SnapshotType.Baseline);
        snapshot.FilePath.Should().Be(filePath);
        snapshot.ChangeDetected.Should().BeFalse();
        snapshot.ChangeScore.Should().Be(0);

        // Verify persisted in DB
        var fromDb = await _db.Snapshots.FindAsync(snapshot.Id);
        fromDb.Should().NotBeNull();
        fromDb!.Type.Should().Be(SnapshotType.Baseline);
    }

    // ── Test 2: GetBaseline returns the most recent baseline file path
    [Fact]
    public async Task GetBaseline_ReturnsMostRecentBaselinePath()
    {
        // Arrange
        await _manager.SaveBaseline(1, "/images/old_baseline.jpg");
        await Task.Delay(10); // ensure different timestamps
        await _manager.SaveBaseline(1, "/images/new_baseline.jpg");

        // Act
        var baselinePath = await _manager.GetBaselinePath(1);

        // Assert
        baselinePath.Should().NotBeNull();
        baselinePath.Should().Be("/images/new_baseline.jpg");
    }

    // ── Test 3: UpdateBaseline replaces existing baseline (deletes old, creates new)
    [Fact]
    public async Task UpdateBaseline_ReplacesExistingBaseline()
    {
        // Arrange
        await _manager.SaveBaseline(1, "/images/old.jpg");

        // Act
        var updated = await _manager.UpdateBaseline(1, "/images/replacement.jpg");

        // Assert
        updated.Should().NotBeNull();
        updated.FilePath.Should().Be("/images/replacement.jpg");
        updated.Type.Should().Be(SnapshotType.Baseline);

        // Old baseline should be removed from DB
        var allSnapshots = await _db.Snapshots.Where(s => s.CameraId == 1).ToListAsync();
        allSnapshots.Should().ContainSingle();
        allSnapshots[0].FilePath.Should().Be("/images/replacement.jpg");

        // GetBaseline should return the new path
        var baselinePath = await _manager.GetBaselinePath(1);
        baselinePath.Should().Be("/images/replacement.jpg");
    }

    // ── Test 4: GetBaseline returns null when no baseline exists
    [Fact]
    public async Task GetBaseline_WhenNoBaselineExists_ReturnsNull()
    {
        // Act
        var baselinePath = await _manager.GetBaselinePath(cameraId: 99);

        // Assert
        baselinePath.Should().BeNull();
    }

    // ── Test 5: SaveBaseline for different cameras keeps separate baselines
    [Fact]
    public async Task SaveBaseline_DifferentCameras_HaveSeparateBaselines()
    {
        // Act
        await _manager.SaveBaseline(1, "/images/cam1.jpg");
        await _manager.SaveBaseline(2, "/images/cam2.jpg");

        // Assert
        var b1 = await _manager.GetBaselinePath(1);
        var b2 = await _manager.GetBaselinePath(2);

        b1.Should().Be("/images/cam1.jpg");
        b2.Should().Be("/images/cam2.jpg");
    }
}
