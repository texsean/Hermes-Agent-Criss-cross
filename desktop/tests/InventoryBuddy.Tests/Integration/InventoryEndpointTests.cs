using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using InventoryBuddy.Hub.Data;
using InventoryBuddy.Shared.Models;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace InventoryBuddy.Tests.Integration;

public class InventoryEndpointTests : IAsyncDisposable
{
    private readonly SqliteConnection _connection;
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public InventoryEndpointTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    services.RemoveAll<DbContextOptions<InventoryDbContext>>();
                    services.RemoveAll<InventoryDbContext>();

                    services.AddDbContext<InventoryDbContext>(options =>
                        options.UseSqlite(_connection));

                    var sp = services.BuildServiceProvider();
                    using var scope = sp.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
                    db.Database.EnsureCreated();
                });
            });

        _client = _factory.CreateClient();
    }

    public async ValueTask DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
        await _connection.DisposeAsync();
    }

    // ── Helper: seed shelves with items ──────────────────────────
    private async Task SeedInventoryAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();

        var shelf1 = new Shelf { Name = "Pantry Shelf 1", Location = "Kitchen" };
        var shelf2 = new Shelf { Name = "Garage Shelf", Location = "Garage" };
        db.Shelves.AddRange(shelf1, shelf2);
        await db.SaveChangesAsync();

        var items = new[]
        {
            new InventoryItem { ShelfId = shelf1.Id, Name = "Baked Beans", Category = "Groceries", Quantity = 3, Status = ItemStatus.Present, LastSeen = DateTime.UtcNow },
            new InventoryItem { ShelfId = shelf1.Id, Name = "Tomato Soup", Category = "Groceries", Quantity = 5, Status = ItemStatus.Present, LastSeen = DateTime.UtcNow },
            new InventoryItem { ShelfId = shelf1.Id, Name = "Socket 9mm", Category = "Tools", Quantity = 1, Status = ItemStatus.Removed, LastSeen = DateTime.UtcNow.AddDays(-2) },
            new InventoryItem { ShelfId = shelf2.Id, Name = "WD-40", Category = "Tools", Quantity = 2, Status = ItemStatus.Present, LastSeen = DateTime.UtcNow },
            new InventoryItem { ShelfId = shelf2.Id, Name = "Duct Tape", Category = "Tools", Quantity = 1, Status = ItemStatus.Depleted, LastSeen = DateTime.UtcNow.AddDays(-7) },
        };
        db.InventoryItems.AddRange(items);
        await db.SaveChangesAsync();
    }

    // ── Test 1: GET /api/inventory returns all shelves as an array
    [Fact]
    public async Task GetInventory_ReturnsAllShelvesAsArray()
    {
        // Arrange
        await SeedInventoryAsync();

        // Act
        var response = await _client.GetAsync("/api/inventory");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        json.ValueKind.Should().Be(JsonValueKind.Array);

        var shelves = json.EnumerateArray().ToList();
        shelves.Should().HaveCount(2);

        // Verify first shelf structure
        var shelf1 = shelves[0];
        shelf1.GetProperty("shelfId").GetInt32().Should().BeGreaterThan(0);
        shelf1.GetProperty("shelfName").GetString().Should().NotBeNullOrEmpty();
        shelf1.GetProperty("items").GetArrayLength().Should().Be(3);
        shelf1.GetProperty("recentAlerts").ValueKind.Should().Be(JsonValueKind.Array);

        // Verify item DTO structure
        var firstItem = shelf1.GetProperty("items")[0];
        firstItem.GetProperty("id").GetInt32().Should().BeGreaterThan(0);
        firstItem.GetProperty("name").GetString().Should().NotBeNullOrEmpty();
        firstItem.GetProperty("quantity").GetInt32().Should().BeGreaterThan(0);
        firstItem.GetProperty("status").GetString().Should().NotBeNullOrEmpty();

        // Second shelf has 2 items
        var shelf2 = shelves[1];
        shelf2.GetProperty("items").GetArrayLength().Should().Be(2);
    }

    // ── Test 2: GET /api/inventory/{shelfId} returns single object (not array)
    [Fact]
    public async Task GetInventory_ByShelfId_ReturnsSingleObject()
    {
        // Arrange
        await SeedInventoryAsync();

        // First, get the shelf IDs
        var allResponse = await _client.GetAsync("/api/inventory");
        var allJson = await allResponse.Content.ReadFromJsonAsync<JsonElement>();
        var shelf1Id = allJson[0].GetProperty("shelfId").GetInt32();

        // Act
        var response = await _client.GetAsync($"/api/inventory/{shelf1Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        json.ValueKind.Should().Be(JsonValueKind.Object);    // Single object, not array!
        json.GetProperty("shelfId").GetInt32().Should().Be(shelf1Id);
        json.GetProperty("items").GetArrayLength().Should().Be(3);

        // Verify all items belong to shelf 1
        var itemNames = json.GetProperty("items").EnumerateArray()
            .Select(i => i.GetProperty("name").GetString())
            .ToList();
        itemNames.Should().Contain(new[] { "Baked Beans", "Tomato Soup", "Socket 9mm" });
        // Verify no shelf 2 items leaked in
        itemNames.Should().NotContain(new[] { "WD-40", "Duct Tape" });
    }

    // ── Test 3: GET /api/inventory/{shelfId} for non-existent shelf → 404
    [Fact]
    public async Task GetInventory_NonExistentShelf_ReturnsNotFound()
    {
        // Act
        var response = await _client.GetAsync("/api/inventory/99999");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── Test 4: GET /api/inventory with no data returns empty array
    [Fact]
    public async Task GetInventory_NoShelves_ReturnsEmptyArray()
    {
        // Act
        var response = await _client.GetAsync("/api/inventory");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        json.ValueKind.Should().Be(JsonValueKind.Array);
        json.GetArrayLength().Should().Be(0);
    }

    // ── Test 5: Verify DaysMissing for removed/depleted items
    [Fact]
    public async Task GetInventory_RemovedItems_HaveDaysMissing()
    {
        // Arrange
        await SeedInventoryAsync();

        // Act
        var response = await _client.GetAsync("/api/inventory");
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();

        // Assert
        var allItems = json.EnumerateArray()
            .SelectMany(s => s.GetProperty("items").EnumerateArray())
            .ToList();

        var depletedItem = allItems.FirstOrDefault(i =>
            i.GetProperty("status").GetString() == "Depleted");
        depletedItem.Should().NotBeNull();
        depletedItem!.TryGetProperty("daysMissing", out var daysMissing).Should().BeTrue();
        daysMissing.GetInt32().Should().BeGreaterThan(0);

        var presentItem = allItems.FirstOrDefault(i =>
            i.GetProperty("status").GetString() == "Present");
        presentItem.Should().NotBeNull();
        // Present items should have null daysMissing (JSON omits the property)
        presentItem!.TryGetProperty("daysMissing", out _).Should().BeFalse();
    }
}
