using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using InventoryBuddy.Hub.Data;
using InventoryBuddy.Shared.DTOs;
using InventoryBuddy.Shared.Models;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SkiaSharp;

namespace InventoryBuddy.Tests.Integration;

public class CameraEndpointTests : IAsyncDisposable
{
    private readonly SqliteConnection _connection;
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public CameraEndpointTests()
    {
        // Use a shared SQLite in-memory connection so the DB lives across requests
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    // Remove the real DbContext registration
                    services.RemoveAll<DbContextOptions<InventoryDbContext>>();
                    services.RemoveAll<InventoryDbContext>();

                    // Add SQLite in-memory
                    services.AddDbContext<InventoryDbContext>(options =>
                        options.UseSqlite(_connection));

                    // Ensure the schema is created
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

    // ── Helper: generate a valid JPEG in memory ──────────────────
    private static byte[] GenerateTestJpeg()
    {
        using var bitmap = new SKBitmap(64, 64);
        bitmap.Erase(SKColors.Orange);
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Jpeg, 80);
        return data.ToArray();
    }

    // ── Helper: seed a camera and shelf ──────────────────────────
    private async Task SeedCameraAsync(string mac = "AA:BB:CC:DD:EE:FF")
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();

        var shelf = new Shelf { Name = "Test Shelf", Location = "Test Location" };
        db.Shelves.Add(shelf);
        await db.SaveChangesAsync();

        var camera = new Camera
        {
            MacAddress = mac,
            Name = "Test Camera",
            ShelfId = shelf.Id,
            IsOnline = true
        };
        db.Cameras.Add(camera);
        await db.SaveChangesAsync();
    }

    // ── Test 1: POST /api/camera/{mac}/image → 200 OK, snapshot saved
    [Fact]
    public async Task PostCameraImage_ValidCamera_ReturnsOkAndSavesSnapshot()
    {
        // Arrange
        const string mac = "11:22:33:44:55:66";
        await SeedCameraAsync(mac);
        var jpegData = GenerateTestJpeg();

        // Act
        using var content = new ByteArrayContent(jpegData);
        content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/jpeg");
        var response = await _client.PostAsync($"/api/camera/{mac}/image", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        json.GetProperty("snapshotId").GetInt32().Should().BeGreaterThan(0);

        // Verify snapshot was saved to DB
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        var snapshots = await db.Snapshots.ToListAsync();
        snapshots.Should().ContainSingle();
    }

    // ── Test 2: POST to non-existent camera MAC → 404
    [Fact]
    public async Task PostCameraImage_UnknownMac_ReturnsNotFound()
    {
        // Arrange
        var jpegData = GenerateTestJpeg();

        // Act
        using var content = new ByteArrayContent(jpegData);
        content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/jpeg");
        var response = await _client.PostAsync("/api/camera/ZZ:ZZ:ZZ:ZZ:ZZ:ZZ/image", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        json.GetProperty("error").GetString().Should().Contain("ZZ:ZZ:ZZ:ZZ:ZZ:ZZ");
    }

    // ── Test 3: GET /api/health → 200 OK
    [Fact]
    public async Task GetHealth_ReturnsOk()
    {
        // Act
        var response = await _client.GetAsync("/api/health");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        json.GetProperty("status").GetString().Should().Be("Healthy");
        json.TryGetProperty("timestamp", out _).Should().BeTrue();
    }

    // ── Test 4: First image upload creates a baseline snapshot
    [Fact]
    public async Task PostCameraImage_FirstUpload_CreatesBaselineSnapshot()
    {
        // Arrange
        const string mac = "AA:11:BB:22:CC:33";
        await SeedCameraAsync(mac);
        var jpegData = GenerateTestJpeg();

        // Act
        using var content = new ByteArrayContent(jpegData);
        content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/jpeg");
        var response = await _client.PostAsync($"/api/camera/{mac}/image", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        var snapshot = await db.Snapshots.FirstAsync();
        snapshot.Type.Should().Be(SnapshotType.Baseline);
    }

    // ── Test 5: POST with empty body returns 400
    [Fact]
    public async Task PostCameraImage_EmptyBody_ReturnsBadRequest()
    {
        // Arrange
        const string mac = "DD:EE:FF:00:11:22";
        await SeedCameraAsync(mac);

        // Act
        using var content = new ByteArrayContent(Array.Empty<byte>());
        content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/jpeg");
        var response = await _client.PostAsync($"/api/camera/{mac}/image", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
