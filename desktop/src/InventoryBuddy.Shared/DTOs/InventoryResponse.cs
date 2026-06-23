namespace InventoryBuddy.Shared.DTOs;

/// <summary>
/// Response returned when listing inventory items.
/// </summary>
public class InventoryResponse
{
    public int ShelfId { get; set; }
    public string ShelfName { get; set; } = string.Empty;
    public List<InventoryItemDto> Items { get; set; } = new();
    public List<AlertDto> RecentAlerts { get; set; } = new();
}

public class InventoryItemDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Category { get; set; }
    public int Quantity { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime LastSeen { get; set; }
    public int? DaysMissing { get; set; }
}

public class AlertDto
{
    public int Id { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public bool IsRead { get; set; }
}
