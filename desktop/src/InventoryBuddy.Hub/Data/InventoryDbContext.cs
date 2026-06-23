using Microsoft.EntityFrameworkCore;
using InventoryBuddy.Shared.Models;

namespace InventoryBuddy.Hub.Data;

public class InventoryDbContext : DbContext
{
    public InventoryDbContext(DbContextOptions<InventoryDbContext> options) : base(options) { }

    public DbSet<Shelf> Shelves => Set<Shelf>();
    public DbSet<Camera> Cameras => Set<Camera>();
    public DbSet<InventoryItem> InventoryItems => Set<InventoryItem>();
    public DbSet<Snapshot> Snapshots => Set<Snapshot>();
    public DbSet<Alert> Alerts => Set<Alert>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Shelf
        modelBuilder.Entity<Shelf>(e =>
        {
            e.HasKey(s => s.Id);
            e.Property(s => s.Name).IsRequired().HasMaxLength(100);
            e.Property(s => s.Location).HasMaxLength(200);
        });

        // Camera
        modelBuilder.Entity<Camera>(e =>
        {
            e.HasKey(c => c.Id);
            e.Property(c => c.MacAddress).IsRequired().HasMaxLength(17);
            e.Property(c => c.Name).HasMaxLength(100);
            e.HasIndex(c => c.MacAddress).IsUnique();
            e.HasOne(c => c.Shelf)
             .WithMany(s => s.Cameras)
             .HasForeignKey(c => c.ShelfId);
        });

        // InventoryItem
        modelBuilder.Entity<InventoryItem>(e =>
        {
            e.HasKey(i => i.Id);
            e.Property(i => i.Name).IsRequired().HasMaxLength(200);
            e.Property(i => i.Category).HasMaxLength(50);
            e.Property(i => i.Status).HasConversion<string>().HasMaxLength(20);
            e.HasOne(i => i.Shelf)
             .WithMany(s => s.Items)
             .HasForeignKey(i => i.ShelfId);
        });

        // Snapshot
        modelBuilder.Entity<Snapshot>(e =>
        {
            e.HasKey(s => s.Id);
            e.Property(s => s.FilePath).IsRequired().HasMaxLength(500);
            e.Property(s => s.Type).HasConversion<string>().HasMaxLength(10);
            e.HasOne(s => s.Camera)
             .WithMany(c => c.Snapshots)
             .HasForeignKey(s => s.CameraId);
        });

        // Alert
        modelBuilder.Entity<Alert>(e =>
        {
            e.HasKey(a => a.Id);
            e.Property(a => a.Type).HasConversion<string>().HasMaxLength(30);
            e.Property(a => a.Severity).HasConversion<string>().HasMaxLength(10);
            e.Property(a => a.Message).IsRequired().HasMaxLength(500);
            e.HasOne(a => a.InventoryItem)
             .WithMany(i => i.Alerts)
             .HasForeignKey(a => a.InventoryItemId);
            e.HasOne(a => a.Snapshot)
             .WithMany()
             .HasForeignKey(a => a.SnapshotId);
        });
    }
}
