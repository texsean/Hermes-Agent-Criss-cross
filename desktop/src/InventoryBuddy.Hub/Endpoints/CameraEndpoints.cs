using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using InventoryBuddy.Hub.Data;
using InventoryBuddy.Hub.Services;
using InventoryBuddy.Shared.Models;

namespace InventoryBuddy.Hub.Endpoints;

/// <summary>
/// Minimal-API handler class for camera-related endpoints.
/// </summary>
public class CameraEndpoints
{
    /// <summary>
    /// POST /api/camera/{mac}/image
    /// Receives a multipart form with an image file, runs change detection,
    /// and creates alerts when something changes.
    /// </summary>
    public async Task<IResult> UploadImage(
        HttpContext ctx,
        string mac,
        InventoryDbContext db,
        BaselineManager baselineManager,
        ChangeDetector changeDetector,
        AlertService alertService,
        IConfiguration config,
        ILogger<CameraEndpoints> logger)
    {
        // ---------- 1. Lookup camera ----------
        var camera = await db.Cameras
            .FirstOrDefaultAsync(c => c.MacAddress == mac);

        if (camera == null)
        {
            logger.LogWarning("Unknown camera MAC: {Mac}", mac);
            return Results.NotFound(new { Error = $"Camera {mac} not registered." });
        }

        // ---------- 2. Read multipart form ----------
        if (!ctx.Request.HasFormContentType)
            return Results.BadRequest(new { Error = "Expected multipart/form-data." });

        var form = await ctx.Request.ReadFormAsync();
        var file = form.Files.GetFile("image");
        if (file == null || file.Length == 0)
            return Results.BadRequest(new { Error = "No 'image' file in form." });

        // ---------- 3. Validate size ----------
        var maxSize = config.GetSection("ImageStorage").GetValue<long>("MaxImageSizeBytes", 5_242_880);
        if (file.Length > maxSize)
            return Results.BadRequest(new { Error = $"Image exceeds max size of {maxSize} bytes." });

        // ---------- 4. Save image to disk ----------
        var basePath = config.GetSection("ImageStorage").GetValue<string>("BasePath", "/data/inventorybuddy/images");
        var cameraDir = Path.Combine(basePath, mac.Replace(":", "-"));
        Directory.CreateDirectory(cameraDir);

        var timestamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff");
        var ext = Path.GetExtension(file.FileName);
        if (string.IsNullOrEmpty(ext)) ext = ".jpg";
        var fileName = $"{timestamp}{ext}";
        var filePath = Path.Combine(cameraDir, fileName);

        await using (var stream = new FileStream(filePath, FileMode.Create))
        {
            await file.CopyToAsync(stream);
        }

        logger.LogInformation("Image saved: {Path} ({Length} bytes)", filePath, file.Length);

        // ---------- 5. Run change detection ----------
        var baselinePath = await baselineManager.GetBaselinePath(camera.Id);
        ChangeResult? changeResult = null;
        Snapshot snapshot;

        if (baselinePath != null && File.Exists(baselinePath))
        {
            changeResult = changeDetector.Compare(baselinePath, filePath);

            snapshot = new Snapshot
            {
                CameraId = camera.Id,
                FilePath = filePath,
                Type = SnapshotType.Regular,
                CapturedAt = DateTime.UtcNow,
                ChangeDetected = changeResult.Changed,
                ChangeScore = changeResult.ChangeScore,
                ChangeRegionsJson = changeResult.Changed
                    ? JsonSerializer.Serialize(changeResult.ChangedRegions)
                    : null
            };

            db.Snapshots.Add(snapshot);
            await db.SaveChangesAsync();

            // ---------- 6. Create alert if change detected ----------
            if (changeResult.Changed)
            {
                var regionCount = changeResult.ChangedRegions.Count;
                await alertService.CreateAlert(
                    AlertType.ChangeDetected,
                    AlertSeverity.Info,
                    $"Camera {camera.Name} detected change (score: {changeResult.ChangeScore:F3}, {regionCount} region(s)).",
                    snapshotId: snapshot.Id
                );
            }

            return Results.Ok(new
            {
                SnapshotId = snapshot.Id,
                CameraId = camera.Id,
                FilePath = filePath,
                Changed = changeResult.Changed,
                ChangeScore = changeResult.ChangeScore,
                ChangedRegions = changeResult.ChangedRegions
            });
        }
        else
        {
            // No baseline yet — save this as the first baseline
            snapshot = await baselineManager.SaveBaseline(camera.Id, filePath);

            logger.LogInformation("No baseline for camera {Cam} — saved first baseline {Id}", camera.Id, snapshot.Id);

            return Results.Ok(new
            {
                SnapshotId = snapshot.Id,
                CameraId = camera.Id,
                FilePath = filePath,
                Changed = false,
                ChangeScore = 0f,
                ChangedRegions = Array.Empty<object>(),
                Note = "First upload — saved as baseline."
            });
        }
    }

    /// <summary>
    /// POST /api/camera/heartbeat
    /// Updates the camera's LastHeartbeat, BatteryVoltage, and online status.
    /// </summary>
    public async Task<IResult> Heartbeat(
        HttpContext ctx,
        InventoryDbContext db,
        ILogger<CameraEndpoints> logger)
    {
        // Expect JSON body: { "mac": "AA:BB:CC:DD:EE:FF", "batteryVoltage": 3.7 }
        using var reader = new StreamReader(ctx.Request.Body);
        var body = await reader.ReadToEndAsync();

        HeartbeatPayload? payload;
        try
        {
            payload = JsonSerializer.Deserialize<HeartbeatPayload>(body);
        }
        catch
        {
            return Results.BadRequest(new { Error = "Invalid JSON body." });
        }

        if (payload == null || string.IsNullOrWhiteSpace(payload.Mac))
            return Results.BadRequest(new { Error = "Field 'mac' is required." });

        var camera = await db.Cameras
            .FirstOrDefaultAsync(c => c.MacAddress == payload.Mac);

        if (camera == null)
            return Results.NotFound(new { Error = $"Camera {payload.Mac} not registered." });

        camera.LastHeartbeat = DateTime.UtcNow;
        camera.BatteryVoltage = payload.BatteryVoltage;
        camera.IsOnline = true;

        await db.SaveChangesAsync();

        logger.LogDebug("Heartbeat from {Mac} — battery {V}V", payload.Mac, payload.BatteryVoltage);

        return Results.Ok(new { Status = "ok", camera.LastHeartbeat, camera.BatteryVoltage });
    }

    private class HeartbeatPayload
    {
        public string Mac { get; set; } = string.Empty;
        public float BatteryVoltage { get; set; }
    }
}
