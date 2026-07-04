using GoCheaper.Trips.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace GoCheaper.Trips.Api.Data;

public class TripsDbContext(DbContextOptions<TripsDbContext> options) : DbContext(options)
{
    public DbSet<Trip>           Trips           => Set<Trip>();
    public DbSet<PickupPoint>    PickupPoints    => Set<PickupPoint>();
    public DbSet<DriverSnapshot> DriverSnapshots => Set<DriverSnapshot>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Trip>(e =>
        {
            e.HasKey(t => t.Id);
            e.Property(t => t.From).HasMaxLength(200).IsRequired();
            e.Property(t => t.To).HasMaxLength(200).IsRequired();
            e.Property(t => t.PricePerSeat).HasPrecision(10, 2);
            e.HasMany(t => t.PickupPoints)
             .WithOne(p => p.Trip)
             .HasForeignKey(p => p.TripId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<PickupPoint>(e =>
        {
            e.HasKey(p => p.Id);
            e.Property(p => p.Address).HasMaxLength(300).IsRequired();
        });

        modelBuilder.Entity<DriverSnapshot>(e =>
        {
            e.HasKey(d => d.DriverId);
            e.Property(d => d.FullName).HasMaxLength(200).IsRequired();
            e.Property(d => d.Email).HasMaxLength(256).IsRequired();
        });
    }
}
