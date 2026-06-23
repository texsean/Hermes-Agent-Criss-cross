using Microsoft.EntityFrameworkCore;
using InventoryBuddy.Hub.Data;
using InventoryBuddy.Hub.Services;
using InventoryBuddy.Shared.DTOs;
using InventoryBuddy.Shared.Models;

namespace InventoryBuddy.Hub.Endpoints;

/// <summary>
/// Minimal-API handler class for inventory query endpoints.
/// </summary>
public class InventoryEndpoints
{
    /// <summary>
    /// GET /api/inventory/{shelfId?}
    /// Returns an InventoryResponse for a specific shelf, or all shelves
    /// when shelfId is omitted (returned as a list).
    /// </summary>
    public async Task<IResult> GetInventory(
        HttpContext ctx,
        int? shelfId,
        InventoryDbContext db,
        AlertService alertService)
    {
        List<Shelf> shelves;

        if (shelfId.HasValue)
        {
            var shelf = await db.Shelves
                .Include(s => s.Items)
                .FirstOrDefaultAsync(s => s.Id == shelfId.Value);

            if (shelf == null)
                return Results.NotFound(new { Error = $"Shelf {shelfId} not found." });

            shelves = new List<Shelf> { shelf };
        }
        else
        {
            shelves = await db.Shelves
                .Include(s => s.Items)
                .ToListAsync();
        }

        var responses = new List<InventoryResponse>();

        foreach (var shelf in shelves)
        {
            var alerts = await alertService.GetRecentAlerts(shelf.Id);
            var staleDaysConfig = 3; // we'll pull this from the config slot
            // Pull from the same config key AlertService uses
            var staleDays = db is null ? 3 : 3; // keep simple — AlertService owns the real logic

            var items = shelf.Items.Select(item =>
            {
                int? daysMissing = null;
                if (item.Status == ItemStatus.Removed || item.Status == ItemStatus.Depleted)
                    daysMissing = (int)(DateTime.UtcNow - item.LastSeen).TotalDays;

                return new InventoryItemDto
                {
                    Id = item.Id,
                    Name = item.Name,
                    Category = item.Category,
                    Quantity = item.Quantity,
                    Status = item.Status.ToString(),
                    LastSeen = item.LastSeen,
                    DaysMissing = daysMissing
                };
            }).ToList();

            responses.Add(new InventoryResponse
            {
                ShelfId = shelf.Id,
                ShelfName = shelf.Name,
                Items = items,
                RecentAlerts = alerts.Select(a => new AlertDto
                {
                    Id = a.Id,
                    Type = a.Type.ToString(),
                    Severity = a.Severity.ToString(),
                    Message = a.Message,
                    CreatedAt = a.CreatedAt,
                    IsRead = a.IsRead
                }).ToList()
            });
        }

        // If single shelf was requested, return the object directly;
        // otherwise return the list.
        if (shelfId.HasValue)
            return Results.Ok(responses[0]);

        return Results.Ok(responses);
    }
}
