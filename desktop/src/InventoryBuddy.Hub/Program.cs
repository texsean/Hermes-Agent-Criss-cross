using Microsoft.EntityFrameworkCore;
using Serilog;
using InventoryBuddy.Hub.Data;
using InventoryBuddy.Hub.Endpoints;
using InventoryBuddy.Hub.Services;

// ---------- Serilog ----------
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.Console()
    .WriteTo.File("/data/inventorybuddy/logs/hub-.log", rollingInterval: RollingInterval.Day)
    .CreateLogger();

try
{
    Log.Information("Starting InventoryBuddy.Hub");

    var builder = WebApplication.CreateBuilder(args);

    // Use Serilog
    builder.Host.UseSerilog();

    // ---------- Services ----------
    var connectionString = builder.Configuration.GetConnectionString("Default")
        ?? "Data Source=/data/inventorybuddy/inventory.db";

    builder.Services.AddDbContext<InventoryDbContext>(opts =>
        opts.UseSqlite(connectionString));

    builder.Services.AddSingleton<BaselineManager>();
    builder.Services.AddSingleton<ChangeDetector>();
    builder.Services.AddScoped<AlertService>();

    var app = builder.Build();

    // ---------- Ensure DB created ----------
    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        db.Database.EnsureCreated();
    }

    // ---------- Endpoints ----------
    var cameraEndpoints = new CameraEndpoints();
    var inventoryEndpoints = new InventoryEndpoints();

    // Camera
    app.MapPost("/api/camera/{mac}/image", cameraEndpoints.UploadImage);
    app.MapPost("/api/camera/heartbeat", cameraEndpoints.Heartbeat);

    // Inventory
    app.MapGet("/api/inventory/{shelfId?}", inventoryEndpoints.GetInventory);

    // Health
    app.MapGet("/api/health", () => Results.Ok(new { Status = "Healthy", Timestamp = DateTime.UtcNow }));

    Log.Information("InventoryBuddy.Hub listening on http://0.0.0.0:5000");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Host terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}

// Expose for integration testing
public partial class Program { }
