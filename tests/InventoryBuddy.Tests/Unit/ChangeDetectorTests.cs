using FluentAssertions;
using InventoryBuddy.Hub.Services;
using SkiaSharp;

namespace InventoryBuddy.Tests.Unit;

public class ChangeDetectorTests
{
    private readonly ChangeDetector _detector = new(diffThreshold: 0.15f);

    // ── Helper: create a solid-color bitmap ──────────────────────
    private static SKBitmap CreateSolidBitmap(int width, int height, SKColor color)
    {
        var bmp = new SKBitmap(width, height);
        bmp.Erase(color);
        return bmp;
    }

    // ── Helper: create a bitmap with a colored rectangle ─────────
    private static SKBitmap CreateBitmapWithRect(int width, int height, SKColor bg, SKColor rectColor, SKRect rect)
    {
        var bmp = new SKBitmap(width, height);
        using var canvas = new SKCanvas(bmp);
        canvas.Clear(bg);
        using var paint = new SKPaint { Color = rectColor, Style = SKPaintStyle.Fill };
        canvas.DrawRect(rect, paint);
        return bmp;
    }

    // ── Test 1: identical images → ChangeScore = 0, Changed = false
    [Fact]
    public void Compare_TwoIdenticalImages_ReturnsZeroChangeScore()
    {
        // Arrange
        using var baseline = CreateSolidBitmap(100, 100, SKColors.Blue);
        using var current = CreateSolidBitmap(100, 100, SKColors.Blue);

        // Act
        var result = _detector.Compare(baseline, current);

        // Assert
        result.ChangeScore.Should().Be(0);
        result.Changed.Should().BeFalse();
    }

    // ── Test 2: completely different images → ChangeScore > 0.5, Changed = true
    [Fact]
    public void Compare_TwoCompletelyDifferentImages_ReturnsHighChangeScore()
    {
        // Arrange
        using var baseline = CreateSolidBitmap(100, 100, SKColors.Black);
        using var current = CreateSolidBitmap(100, 100, SKColors.White);

        // Act
        var result = _detector.Compare(baseline, current);

        // Assert
        result.ChangeScore.Should().BeGreaterThan(0.5);
        result.Changed.Should().BeTrue();
    }

    // ── Test 3: ~10% of pixels differ → ChangeScore near 0.10
    [Fact]
    public void Compare_TenPercentPixelsDiffer_ReturnsChangeScoreNearPointTen()
    {
        // Arrange — 100x100 bitmap, draw a 31x31 rect (~10% area)
        using var baseline = CreateSolidBitmap(100, 100, SKColors.Gray);
        using var current = CreateBitmapWithRect(100, 100, SKColors.Gray, SKColors.Red, new SKRect(0, 0, 32, 32));

        // Act
        var result = _detector.Compare(baseline, current);

        // Assert
        result.ChangeScore.Should().BeApproximately(0.10f, 0.03f);
    }

    // ── Test 4: null baseline → throws ArgumentNullException
    [Fact]
    public void Compare_NullBaseline_ThrowsArgumentNullException()
    {
        // Arrange
        using var current = CreateSolidBitmap(100, 100, SKColors.Blue);

        // Act
        var act = () => _detector.Compare(null, current);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("baseline");
    }

    // ── Test 5: images of different dimensions → should resize and compare
    [Fact]
    public void Compare_DifferentDimensions_ResizesAndCompares()
    {
        // Arrange
        using var baseline = CreateSolidBitmap(50, 50, SKColors.Green);
        using var current = CreateSolidBitmap(200, 200, SKColors.Green);  // Same color, different size

        // Act
        var result = _detector.Compare(baseline, current);

        // Assert — after resize, identical color → zero change
        result.ChangeScore.Should().Be(0);
        result.Changed.Should().BeFalse();
    }

    // ── Test 6: different dimensions, different content → detects change
    [Fact]
    public void Compare_DifferentDimensionsDifferentContent_DetectsChange()
    {
        // Arrange
        using var baseline = CreateSolidBitmap(50, 50, SKColors.Green);
        using var current = CreateSolidBitmap(200, 200, SKColors.Red);

        // Act
        var result = _detector.Compare(baseline, current);

        // Assert
        result.ChangeScore.Should().BeGreaterThan(0.5);
        result.Changed.Should().BeTrue();
    }
}
