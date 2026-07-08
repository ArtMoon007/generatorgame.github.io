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
    public DbSet<PvpRating> PvpRatings => Set<PvpRating>();
    public DbSet<PvpQueueEntry> PvpQueueEntries => Set<PvpQueueEntry>();
    public DbSet<PvpMatch> PvpMatches => Set<PvpMatch>();
    public DbSet<PvpRound> PvpRounds => Set<PvpRound>();
    public DbSet<PvpDuelInvite> PvpDuelInvites => Set<PvpDuelInvite>();

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

        b.Entity<PvpRating>()
            .HasOne(r => r.User)
            .WithMany()
            .HasForeignKey(r => r.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        b.Entity<PvpRating>()
            .HasIndex(r => new { r.UserId, r.Generator })
            .IsUnique();

        b.Entity<PvpQueueEntry>()
            .HasOne(q => q.User)
            .WithMany()
            .HasForeignKey(q => q.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        b.Entity<PvpQueueEntry>()
            .HasIndex(q => new { q.UserId, q.Generator })
            .IsUnique();

        b.Entity<PvpQueueEntry>()
            .HasIndex(q => new { q.Generator, q.RankIndex, q.CreatedAt });

        b.Entity<PvpMatch>()
            .HasOne(m => m.Player1)
            .WithMany()
            .HasForeignKey(m => m.Player1Id)
            .OnDelete(DeleteBehavior.Cascade);

        b.Entity<PvpMatch>()
            .HasOne(m => m.Player2)
            .WithMany()
            .HasForeignKey(m => m.Player2Id)
            .OnDelete(DeleteBehavior.Cascade);

        b.Entity<PvpRound>()
            .HasOne(r => r.Match)
            .WithMany()
            .HasForeignKey(r => r.MatchId)
            .OnDelete(DeleteBehavior.Cascade);

        b.Entity<PvpRound>()
            .HasIndex(r => new { r.MatchId, r.RoundNumber })
            .IsUnique();

        b.Entity<PvpDuelInvite>()
            .HasOne(i => i.SenderUser)
            .WithMany()
            .HasForeignKey(i => i.SenderUserId)
            .OnDelete(DeleteBehavior.Cascade);

        b.Entity<PvpDuelInvite>()
            .HasOne(i => i.TargetUser)
            .WithMany()
            .HasForeignKey(i => i.TargetUserId)
            .OnDelete(DeleteBehavior.Cascade);

        b.Entity<PvpDuelInvite>()
            .HasIndex(i => new { i.TargetUserId, i.Status, i.ExpiresAt });
    }
}
