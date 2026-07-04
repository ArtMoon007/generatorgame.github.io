using GeneratorGame.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace GeneratorGame.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<Score> Scores => Set<Score>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);

        b.Entity<User>()
            .HasIndex(u => u.Username)
            .IsUnique();

        b.Entity<User>()
            .HasIndex(u => u.RobloxId)
            .IsUnique()
            .HasFilter("[RobloxId] IS NOT NULL");

        b.Entity<Score>()
            .HasOne(s => s.User)
            .WithMany(u => u.Scores)
            .HasForeignKey(s => s.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Ускоряет выборку лидерборда по конкретному генератору
        b.Entity<Score>()
            .HasIndex(s => new { s.Generator, s.TimeMs });
    }
}
