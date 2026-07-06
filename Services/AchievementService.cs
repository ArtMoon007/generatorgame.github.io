using GeneratorGame.Data;
using GeneratorGame.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace GeneratorGame.Services;

public class AchievementService
{
    private const int BaseGameExperience = 20;
    private readonly AppDbContext _db;

    public AchievementService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<AchievementResult> RecordGameAsync(int userId, string generator, long timeMs, int? rank)
    {
        var stat = await _db.UserStats.FirstOrDefaultAsync(s => s.UserId == userId);
        if (stat == null)
        {
            stat = new UserStat
            {
                UserId = userId,
                Level = 1
            };
            _db.UserStats.Add(stat);
        }

        var scoreTotals = await _db.Scores
            .Where(s => s.UserId == userId)
            .GroupBy(s => s.UserId)
            .Select(g => new
            {
                Games = g.Count(),
                TotalMs = g.Sum(s => s.TimeMs)
            })
            .FirstOrDefaultAsync();

        var playedGenerators = await _db.Scores
            .Where(s => s.UserId == userId)
            .GroupBy(s => s.Generator)
            .Select(g => new { Generator = g.Key, Count = g.Count() })
            .ToListAsync();

        stat.FavoriteGenerator = playedGenerators
            .OrderByDescending(g => g.Count)
            .ThenBy(g => g.Generator)
            .Select(g => g.Generator)
            .FirstOrDefault() ?? generator;
        stat.GamesPlayed = scoreTotals?.Games ?? 0;
        stat.Wins = scoreTotals?.Games ?? 0;
        stat.TotalPlayTimeMs = scoreTotals?.TotalMs ?? 0;
        stat.Experience += BaseGameExperience;
        stat.UpdatedAt = DateTime.UtcNow;

        var existingKeys = await _db.UserAchievements
            .Where(a => a.UserId == userId)
            .Select(a => a.Key)
            .ToListAsync();

        var existing = existingKeys.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var unlocked = new List<UserAchievement>();

        await TryUnlock("first_game");
        if (stat.GamesPlayed >= 5) await TryUnlock("five_games");
        if (stat.GamesPlayed >= 20) await TryUnlock("twenty_games");
        if (generator == "bitebynight") await TryUnlock("first_bbn");
        if (generator == "forsaken") await TryUnlock("first_forsaken");
        if (generator == "bitebynight" && rank is > 0 and <= 10) await TryUnlock("bbn_top_10");
        if (generator == "forsaken" && rank is > 0 and <= 10) await TryUnlock("forsaken_top_10");
        if (timeMs <= 30_000) await TryUnlock("sub_30");
        if (timeMs <= 10_000) await TryUnlock("sub_10");
        if (timeMs <= 5_000) await TryUnlock("sub_5");

        stat.Level = CalculateLevel(stat.Experience);
        if (stat.Level >= 5) await TryUnlock("level_5");
        if (stat.Level >= 10) await TryUnlock("level_10");
        stat.Level = CalculateLevel(stat.Experience);

        return new AchievementResult(
            stat.Level,
            stat.Experience,
            ExperienceForNextLevel(stat.Level),
            unlocked.Select(ToDto).ToList());

        Task TryUnlock(string key)
        {
            if (existing.Contains(key)) return Task.CompletedTask;

            var def = AchievementCatalog.Get(key);
            if (def == null) return Task.CompletedTask;

            existing.Add(key);
            stat.Experience += def.Experience;

            var achievement = new UserAchievement
            {
                UserId = userId,
                Key = def.Key,
                Title = def.Title,
                Description = def.Description,
                Icon = def.Icon,
                Experience = def.Experience,
                UnlockedAt = DateTime.UtcNow
            };

            _db.UserAchievements.Add(achievement);
            unlocked.Add(achievement);
            return Task.CompletedTask;
        }
    }

    public static int CalculateLevel(int experience)
    {
        var level = 1;

        while (level < 100 && experience >= ExperienceForLevel(level + 1))
        {
            level++;
        }

        return level;
    }

    public static int ExperienceForLevel(int level)
    {
        if (level <= 1) return 0;
        return (level - 1) * (level - 1) * 100;
    }

    public static int ExperienceForNextLevel(int level)
    {
        if (level >= 100) return ExperienceForLevel(100);
        return ExperienceForLevel(level + 1);
    }

    private static AchievementDto ToDto(UserAchievement achievement) =>
        new(
            achievement.Key,
            achievement.Title,
            achievement.Description,
            achievement.Icon,
            achievement.Experience,
            achievement.UnlockedAt);
}

public record AchievementResult(
    int Level,
    int Experience,
    int NextLevelExperience,
    IReadOnlyList<AchievementDto> NewAchievements);

public record AchievementDto(
    string Key,
    string Title,
    string Description,
    string Icon,
    int Experience,
    DateTime UnlockedAt);
