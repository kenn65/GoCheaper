using GoCheaper.Booking.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace GoCheaper.Booking.Api.Data;

public class BookingDbContext(DbContextOptions<BookingDbContext> options) : DbContext(options)
{
    public DbSet<TripSnapshot>    TripSnapshots   => Set<TripSnapshot>();
    public DbSet<PassengerBooking> Bookings       => Set<PassengerBooking>();
    public DbSet<DriverSnapshot>  DriverSnapshots => Set<DriverSnapshot>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TripSnapshot>(e =>
        {
            e.HasKey(t => t.TripId);
            e.Property(t => t.DriverFullName).HasMaxLength(200).IsRequired();
            e.Property(t => t.DriverEmail).HasMaxLength(256).IsRequired();
            e.Property(t => t.From).HasMaxLength(200).IsRequired();
            e.Property(t => t.To).HasMaxLength(200).IsRequired();
            e.Property(t => t.PricePerSeat).HasPrecision(10, 2);
            e.HasMany(t => t.Bookings)
             .WithOne(b => b.Trip)
             .HasForeignKey(b => b.TripId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<PassengerBooking>(e =>
        {
            e.HasKey(b => b.Id);
            e.Property(b => b.PassengerFullName).HasMaxLength(200).IsRequired();
            e.HasIndex(b => new { b.TripId, b.PassengerUserId }).IsUnique();
        });

        modelBuilder.Entity<DriverSnapshot>(e =>
        {
            e.HasKey(d => d.DriverId);
            e.Property(d => d.FullName).HasMaxLength(200).IsRequired();
            e.Property(d => d.Email).HasMaxLength(256).IsRequired();
        });
    }
}
