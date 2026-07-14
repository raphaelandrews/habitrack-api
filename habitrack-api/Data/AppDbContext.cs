using Habitrack.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Habitrack.Api.Data;

public class AppDbContext : DbContext
{
    public DbSet<Habit> Habits => Set<Habit>();
    public DbSet<HabitCompletion> HabitCompletions => Set<HabitCompletion>();
    public DbSet<User> Users => Set<User>();

    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Habit>(entity =>
        {
            entity.HasKey(h => h.Id);
            entity.Property(h => h.Name).HasMaxLength(100).IsRequired();
            entity.Property(h => h.Color).HasMaxLength(7).HasDefaultValue("#3B82F6");
            entity.HasIndex(h => h.UserId);

            entity.HasMany(h => h.Completions)
                  .WithOne(c => c.Habit)
                  .HasForeignKey(c => c.HabitId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasQueryFilter(h => h.IsActive);
        });

        modelBuilder.Entity<HabitCompletion>(entity =>
        {
            entity.HasKey(c => c.Id);
            entity.HasIndex(c => new { c.HabitId, c.Date }).IsUnique();
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(u => u.Id);
            entity.HasIndex(u => u.Email).IsUnique();
            entity.Property(u => u.Email).HasMaxLength(255).IsRequired();
        });
    }
}
