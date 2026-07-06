using GeneratorGame.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace GeneratorGame.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<Score> Scores => Set<Score>();
    public DbSet<UserStat> UserStats => Set<UserStat>();
    public DbSet<UserAchievement> UserAchievements => Set<UserAchievement>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);

        b.Entity<User>()
            .HasIndex(u => u.Username)
            .IsUnique();

        b.Entity<User>()
            .HasIndex(u => u.Email)
            .IsUnique();

        b.Entity<User>()
            .HasIndex(u => u.RobloxId)
            .IsUnique();

        b.Entity<Score>()
            .HasOne(s => s.User)
            .WithMany(u => u.Scores)
            .HasForeignKey(s => s.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Ускоряет выборку лидерборда по конкретному генератору
        b.Entity<Score>()
            .HasIndex(s => new { s.Generator, s.TimeMs });

        b.Entity<UserStat>()
            .HasOne(s => s.User)
            .WithOne(u => u.Stat)
            .HasForeignKey<UserStat>(s => s.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        b.Entity<UserStat>()
            .HasIndex(s => s.UserId)
            .IsUnique();

        b.Entity<UserAchievement>()
            .HasOne(a => a.User)
            .WithMany(u => u.Achievements)
            .HasForeignKey(a => a.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        b.Entity<UserAchievement>()
            .HasIndex(a => new { a.UserId, a.Key })
            .IsUnique();
    }
}
