using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using InventoryBuddy.Hub.Data;
using InventoryBuddy.Shared.Models;

namespace InventoryBuddy.Hub.Services;

/// <summary>
/// Creates, queries, and resolves alerts.
/// Also handles the background stale-item check.
/// </summary>
public class AlertService : IAlertService
{
    private readonly InventoryDbContext _db;
    private readonly int _staleItemDays;
    private readonly ILogger<AlertService> _logger;

    public AlertService(InventoryDbContext db, IConfiguration configuration, ILogger<AlertService> logger)
    {
        _db = db;
        _logger = logger;
        _staleItemDays = configuration.GetSection("Alerts").GetValue<int>("StaleItemDays", 3);
    }

    /// <summary>
    /// Create and persist a new alert.
    /// </summary>
    public async Task<Alert> CreateAlert(
        AlertType type,
        AlertSeverity severity,
        string message,
        int? inventoryItemId = null,
        int? snapshotId = null)
    {
        var alert = new Alert
        {
            Type = type,
            Severity = severity,
            Message = message,
            InventoryItemId = inventoryItemId,
            SnapshotId = snapshotId,
            IsRead = false,
            CreatedAt = DateTime.UtcNow
        };

        _db.Alerts.Add(alert);
        await _db.SaveChangesAsync();

        _logger.LogInformation("Alert created: {Type} {Severity} — {Message}", type, severity, message);
        return alert;
    }

    /// <summary>
    /// Scan all InventoryItems with Status=Removed whose LastSeen is older
    /// than StaleItemDays.  Mark them Depleted and fire ItemMissing alerts.
    /// </summary>
    public async Task<int> CheckStaleItems()
    {
        var cutoff = DateTime.UtcNow.AddDays(-_staleItemDays);

        var staleItems = await _db.InventoryItems
            .Where(i => i.Status == ItemStatus.Removed && i.LastSeen < cutoff)
            .ToListAsync();

        int count = 0;
        foreach (var item in staleItems)
        {
            item.Status = ItemStatus.Depleted;

            await CreateAlert(
                AlertType.ItemMissing,
                AlertSeverity.Warning,
                $"\"{item.Name}\" has been missing for {_staleItemDays}+ days and is now marked Depleted.",
                inventoryItemId: item.Id
            );

            count++;
        }

        if (count > 0)
            await _db.SaveChangesAsync();

        _logger.LogInformation("Stale-item check: {Count} items marked Depleted", count);
        return count;
    }

    /// <summary>
    /// Return recent unread alerts, optionally scoped to a shelf.
    /// </summary>
    public async Task<List<Alert>> GetRecentAlerts(int? shelfId = null, int take = 50)
    {
        var query = _db.Alerts
            .Include(a => a.InventoryItem)
            .Where(a => !a.IsRead)
            .OrderByDescending(a => a.CreatedAt)
            .AsQueryable();

        if (shelfId.HasValue)
        {
            query = query.Where(a =>
                a.InventoryItem != null && a.InventoryItem.ShelfId == shelfId.Value);
        }

        return await query.Take(take).ToListAsync();
    }
}
