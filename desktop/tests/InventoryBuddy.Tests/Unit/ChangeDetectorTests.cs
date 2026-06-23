using FluentAssertions;
using InventoryBuddy.Hub.Services;
using Microsoft.Extensions.Configuration;
using SkiaSharp;

namespace InventoryBuddy.Tests.Unit;

public class ChangeDetectorTests : IDisposable
{
    private readonly string _tempDir;
    private readonly ChangeDetector _detector;

    public ChangeDetectorTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"invbuddy_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ChangeDetection:DiffThreshold"] = "0.15",
                ["ChangeDetection:MinChangedPixels"] = "500"
            })
            .Build();

        _detector = new ChangeDetector(config);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { /* ignore */ }
    }

    // ── Helpers for generating test JPEGs in temp files ──────────

    private string SaveTestJpeg(string name, int width, int height, SKColor color)
    {
        using var bmp = new SKBitmap(width, height);
        bmp.Erase(color);
        return SaveBitmapAsJpeg(name, bmp);
    }

    private string SaveTestJpegWithRect(string name, int width, int height, SKColor bg, SKColor rectColor, SKRect rect)
    {
        using var bmp = new SKBitmap(width, height);
        using var canvas = new SKCanvas(bmp);
        canvas.Clear(bg);
        using var paint = new SKPaint { Color = rectColor, Style = SKPaintStyle.Fill };
        canvas.DrawRect(rect, paint);
        return SaveBitmapAsJpeg(name, bmp);
    }

    private string SaveBitmapAsJpeg(string name, SKBitmap bitmap)
    {
        var path = Path.Combine(_tempDir, name);
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Jpeg, 90);
        using var fs = File.Create(path);
        data.SaveTo(fs);
        return path;
    }

    // ── Test 1: two identical images → ChangeScore = 0, Changed = false
    [Fact]
    public void Compare_TwoIdenticalImages_ReturnsZeroChangeScore()
    {
        // Arrange
        var baselinePath = SaveTestJpeg("baseline.jpg", 640, 480, SKColors.Blue);
        var currentPath = SaveTestJpeg("current.jpg", 640, 480, SKColors.Blue);

        // Act
        var result = _detector.Compare(baselinePath, currentPath);

        // Assert
        result.ChangeScore.Should().Be(0);
        result.Changed.Should().BeFalse();
        result.ChangedRegions.Should().BeEmpty();
    }

    // ── Test 2: two completely different images → ChangeScore > 0.5, Changed = true
    [Fact]
    public void Compare_TwoCompletelyDifferentImages_ReturnsHighChangeScore()
    {
        // Arrange
        var baselinePath = SaveTestJpeg("baseline.jpg", 640, 480, SKColors.Black);
        var currentPath = SaveTestJpeg("current.jpg", 640, 480, SKColors.White);

        // Act
        var result = _detector.Compare(baselinePath, currentPath);

        // Assert
        result.ChangeScore.Should().BeGreaterThan(0.5);
        result.Changed.Should().BeTrue();
    }

    // ── Test 3: ~10% of pixels differ → ChangeScore near 0.10
    [Fact]
    public void Compare_TenPercentPixelsDiffer_ReturnsChangeScoreNearPointTen()
    {
        // Arrange — 640x480 bitmap; draw a rect ~10% of area (202x152 ≈ 10%)
        var baselinePath = SaveTestJpeg("baseline.jpg", 640, 480, SKColors.Gray);
        var currentPath = SaveTestJpegWithRect("current.jpg", 640, 480,
            SKColors.Gray, SKColors.Red, new SKRect(0, 0, 202, 152));

        // Act
        var result = _detector.Compare(baselinePath, currentPath);

        // Assert
        result.ChangeScore.Should().BeApproximately(0.10f, 0.04f);
    }

    // ── Test 4: non-existent baseline file → Changed = false, score = 0
    [Fact]
    public void Compare_MissingBaselineFile_ReturnsNoChange()
    {
        // Arrange
        var baselinePath = Path.Combine(_tempDir, "nonexistent.jpg");
        var currentPath = SaveTestJpeg("current.jpg", 640, 480, SKColors.Blue);

        // Act
        var result = _detector.Compare(baselinePath, currentPath);

        // Assert
        result.Changed.Should().BeFalse();
        result.ChangeScore.Should().Be(0);
    }

    // ── Test 5: images of different original dimensions → both resized to 640x480 and compared
    [Fact]
    public void Compare_DifferentOriginalDimensions_ResizesAndCompares()
    {
        // Arrange — different sizes but same color after resize
        var baselinePath = SaveTestJpeg("baseline.jpg", 320, 240, SKColors.Green);
        var currentPath = SaveTestJpeg("current.jpg", 1280, 960, SKColors.Green);

        // Act
        var result = _detector.Compare(baselinePath, currentPath);

        // Assert — both resized to 640x480, identical color → no change
        result.ChangeScore.Should().Be(0);
        result.Changed.Should().BeFalse();
    }

    // ── Test 6: different dimensions + different content → detects change
    [Fact]
    public void Compare_DifferentDimensionsDifferentContent_DetectsChange()
    {
        // Arrange
        var baselinePath = SaveTestJpeg("baseline.jpg", 320, 240, SKColors.Green);
        var currentPath = SaveTestJpeg("current.jpg", 1280, 960, SKColors.Red);

        // Act
        var result = _detector.Compare(baselinePath, currentPath);

        // Assert
        result.ChangeScore.Should().BeGreaterThan(0.5);
        result.Changed.Should().BeTrue();
    }
}
