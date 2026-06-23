using System.Drawing;
using Microsoft.Extensions.Configuration;
using SkiaSharp;

namespace InventoryBuddy.Hub.Services;

/// <summary>
/// Compares two shelf images pixel-by-pixel using SkiaSharp and
/// returns a ChangeResult with score, flag, and bounding boxes.
/// </summary>
public class ChangeDetector
{
    private readonly float _diffThreshold;
    private readonly int _minChangedPixels;
    private const int TargetWidth = 640;
    private const int TargetHeight = 480;

    public ChangeDetector(IConfiguration configuration)
    {
        var section = configuration.GetSection("ChangeDetection");
        _diffThreshold = section.GetValue<float>("DiffThreshold", 0.15f);
        _minChangedPixels = section.GetValue<int>("MinChangedPixels", 500);
    }

    /// <summary>
    /// Compare baseline and current images.  Both are resized to 640×480
    /// for consistent comparison.  Returns a ChangeResult.
    /// </summary>
    public ChangeResult Compare(string baselinePath, string currentPath)
    {
        using var baselineBmp = LoadAndResize(baselinePath);
        using var currentBmp = LoadAndResize(currentPath);

        if (baselineBmp == null || currentBmp == null)
            return new ChangeResult { Changed = false, ChangeScore = 0 };

        int width = baselineBmp.Width;
        int height = baselineBmp.Height;
        int totalPixels = width * height;

        // Build a bool grid of "changed" pixels
        var changedMask = new bool[width, height];
        int changedCount = 0;

        var baselinePixels = baselineBmp.Pixels;
        var currentPixels = currentBmp.Pixels;

        // Both bitmaps are the same size after resize
        for (int i = 0; i < totalPixels; i++)
        {
            int x = i % width;
            int y = i / width;

            var bp = baselinePixels[i];
            var cp = currentPixels[i];

            // Per-channel difference normalised to 0..1
            float dr = Math.Abs(bp.Red - cp.Red) / 255f;
            float dg = Math.Abs(bp.Green - cp.Green) / 255f;
            float db = Math.Abs(bp.Blue - cp.Blue) / 255f;
            float avgDiff = (dr + dg + db) / 3f;

            if (avgDiff > _diffThreshold)
            {
                changedMask[x, y] = true;
                changedCount++;
            }
        }

        float changeScore = totalPixels > 0 ? (float)changedCount / totalPixels : 0;
        bool changed = changedCount >= _minChangedPixels;

        var regions = changed
            ? ExtractChangedRegions(changedMask, width, height)
            : new List<Rectangle>();

        return new ChangeResult
        {
            Changed = changed,
            ChangeScore = changeScore,
            ChangedRegions = regions
        };
    }

    // ---------- helpers ----------

    /// <summary>
    /// Load a JPEG/PNG from disk and resize to 640×480.
    /// Returns null on any failure.
    /// </summary>
    private SKBitmap? LoadAndResize(string path)
    {
        try
        {
            using var stream = new SKFileStream(path);
            using var codec = SKCodec.Create(stream);
            var info = new SKImageInfo(TargetWidth, TargetHeight);
            var bmp = SKBitmap.Decode(codec, info);
            return bmp;
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "ChangeDetector: failed to load image {Path}", path);
            return null;
        }
    }

    /// <summary>
    /// Simple connected-component / bounding-box clustering.
    /// Performs a flood-fill scan of the boolean mask, merges small
    /// clusters, and returns a list of bounding rectangles.
    /// </summary>
    private List<Rectangle> ExtractChangedRegions(bool[,] mask, int width, int height)
    {
        var visited = new bool[width, height];
        var regions = new List<Rectangle>();

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                if (!mask[x, y] || visited[x, y])
                    continue;

                // Flood-fill this component
                int minX = x, maxX = x, minY = y, maxY = y;
                var stack = new Stack<(int, int)>();
                stack.Push((x, y));
                visited[x, y] = true;

                while (stack.Count > 0)
                {
                    var (cx, cy) = stack.Pop();

                    if (cx < minX) minX = cx;
                    if (cx > maxX) maxX = cx;
                    if (cy < minY) minY = cy;
                    if (cy > maxY) maxY = cy;

                    // 4-connected neighbours
                    foreach (var (nx, ny) in new[] { (cx - 1, cy), (cx + 1, cy), (cx, cy - 1), (cx, cy + 1) })
                    {
                        if (nx >= 0 && nx < width && ny >= 0 && ny < height
                            && mask[nx, ny] && !visited[nx, ny])
                        {
                            visited[nx, ny] = true;
                            stack.Push((nx, ny));
                        }
                    }
                }

                int w = maxX - minX + 1;
                int h = maxY - minY + 1;

                // Filter out tiny noise regions (< 4 px in either dimension)
                if (w >= 4 && h >= 4)
                    regions.Add(new Rectangle(minX, minY, w, h));
            }
        }

        // Merge overlapping regions
        return MergeRegions(regions);
    }

    private static List<Rectangle> MergeRegions(List<Rectangle> regions)
    {
        if (regions.Count <= 1)
            return regions;

        bool merged;
        do
        {
            merged = false;
            for (int i = 0; i < regions.Count; i++)
            {
                for (int j = i + 1; j < regions.Count; j++)
                {
                    var r1 = regions[i];
                    var r2 = regions[j];

                    // Expand by 10 px to allow near-touching rectangles to merge
                    var expanded1 = new Rectangle(r1.X - 5, r1.Y - 5, r1.Width + 10, r1.Height + 10);
                    if (expanded1.IntersectsWith(r2))
                    {
                        int x = Math.Min(r1.X, r2.X);
                        int y = Math.Min(r1.Y, r2.Y);
                        int r = Math.Max(r1.X + r1.Width, r2.X + r2.Width);
                        int b = Math.Max(r1.Y + r1.Height, r2.Y + r2.Height);
                        regions[i] = new Rectangle(x, y, r - x, b - y);
                        regions.RemoveAt(j);
                        merged = true;
                        break;
                    }
                }
                if (merged) break;
            }
        } while (merged);

        return regions;
    }
}
