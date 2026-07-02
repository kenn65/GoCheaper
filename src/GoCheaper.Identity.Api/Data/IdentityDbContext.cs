using GoCheaper.Identity.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace GoCheaper.Identity.Api.Data;

public class IdentityDbContext(DbContextOptions<IdentityDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(u => u.Id);
            entity.Property(u => u.FirstName).HasMaxLength(100).IsRequired();
            entity.Property(u => u.LastName).HasMaxLength(100).IsRequired();
            entity.Property(u => u.Email).HasMaxLength(256).IsRequired();
            entity.HasIndex(u => u.Email).IsUnique();
            entity.HasIndex(u => new { u.FirstName, u.LastName }).IsUnique();
            entity.Property(u => u.PasswordHash).IsRequired();
            entity.Property(u => u.DriverPictureBase64).HasColumnType("nvarchar(max)");
            entity.Property(u => u.MobilePhone).HasMaxLength(20);
        });
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        foreach (var entry in ChangeTracker.Entries<User>().Where(e => e.State == EntityState.Added))
            entry.Entity.CreatedAt = DateTime.UtcNow;

        return base.SaveChangesAsync(cancellationToken);
    }
}
