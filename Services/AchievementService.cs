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
        if (_db.Database.IsNpgsql())
        {
            var postgresUnlocked = await BackfillPostgresAsync(userId);
            var postgresStat = await _db.UserStats
                .Where(s => s.UserId == userId)
                .Select(s => new { s.Level, s.Experience })
                .FirstOrDefaultAsync();

            var experience = postgresStat?.Experience ?? 0;
            var level = postgresStat?.Level ?? CalculateLevel(experience);

            return new AchievementResult(
                level,
                experience,
                ExperienceForNextLevel(level),
                postgresUnlocked);
        }

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

    public async Task<IReadOnlyList<AchievementDto>> BackfillAsync(int userId)
    {
        if (_db.Database.IsNpgsql())
        {
            return await BackfillPostgresAsync(userId);
        }

        var scores = await _db.Scores
            .Where(s => s.UserId == userId)
            .Select(s => new { s.Generator, s.TimeMs })
            .ToListAsync();

        if (scores.Count == 0) return Array.Empty<AchievementDto>();

        var stat = await _db.UserStats.FirstOrDefaultAsync(s => s.UserId == userId);
        if (stat == null)
        {
            stat = new UserStat { UserId = userId, Level = 1 };
            _db.UserStats.Add(stat);
        }

        var favorite = scores
            .GroupBy(s => s.Generator)
            .OrderByDescending(g => g.Count())
            .ThenBy(g => g.Key)
            .Select(g => g.Key)
            .FirstOrDefault() ?? "bitebynight";

        stat.GamesPlayed = scores.Count;
        stat.Wins = scores.Count;
        stat.TotalPlayTimeMs = scores.Sum(s => s.TimeMs);
        stat.FavoriteGenerator = favorite;
        stat.Experience = Math.Max(stat.Experience, scores.Count * BaseGameExperience);
        stat.UpdatedAt = DateTime.UtcNow;

        var existingKeys = await _db.UserAchievements
            .Where(a => a.UserId == userId)
            .Select(a => a.Key)
            .ToListAsync();

        var existing = existingKeys.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var unlocked = new List<UserAchievement>();
        var bestTime = scores.Min(s => s.TimeMs);
        var bbnRank = await GetRankAsync(userId, "bitebynight");
        var forsakenRank = await GetRankAsync(userId, "forsaken");

        TryUnlock("first_game");
        if (scores.Count >= 5) TryUnlock("five_games");
        if (scores.Count >= 20) TryUnlock("twenty_games");
        if (scores.Any(s => s.Generator == "bitebynight")) TryUnlock("first_bbn");
        if (scores.Any(s => s.Generator == "forsaken")) TryUnlock("first_forsaken");
        if (bbnRank is > 0 and <= 10) TryUnlock("bbn_top_10");
        if (forsakenRank is > 0 and <= 10) TryUnlock("forsaken_top_10");
        if (bestTime <= 30_000) TryUnlock("sub_30");
        if (bestTime <= 10_000) TryUnlock("sub_10");
        if (bestTime <= 5_000) TryUnlock("sub_5");

        stat.Level = CalculateLevel(stat.Experience);
        if (stat.Level >= 5) TryUnlock("level_5");
        if (stat.Level >= 10) TryUnlock("level_10");
        stat.Level = CalculateLevel(stat.Experience);

        return unlocked.Select(ToDto).ToList();

        void TryUnlock(string key)
        {
            if (existing.Contains(key)) return;

            var def = AchievementCatalog.Get(key);
            if (def == null) return;

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
        }
    }

    private async Task<int?> GetRankAsync(int userId, string generator)
    {
        var myBest = await _db.Scores
            .Where(s => s.UserId == userId && s.Generator == generator)
            .MinAsync(s => (long?)s.TimeMs);

        if (myBest == null) return null;

        var betterPlayers = await _db.Scores
            .Where(s => s.Generator == generator)
            .GroupBy(s => s.UserId)
            .Select(g => new { UserId = g.Key, BestMs = g.Min(s => s.TimeMs) })
            .CountAsync(x => x.BestMs < myBest.Value);

        return betterPlayers + 1;
    }

    private async Task<IReadOnlyList<AchievementDto>> BackfillPostgresAsync(int userId)
    {
        var scores = await _db.Scores
            .Where(s => s.UserId == userId)
            .Select(s => new ScoreLite(s.Generator, s.TimeMs))
            .ToListAsync();

        if (scores.Count == 0) return Array.Empty<AchievementDto>();

        var favorite = scores
            .GroupBy(s => s.Generator)
            .OrderByDescending(g => g.Count())
            .ThenBy(g => g.Key)
            .Select(g => g.Key)
            .FirstOrDefault() ?? "bitebynight";

        var bestTime = scores.Min(s => s.TimeMs);
        var bbnRank = await GetRankAsync(userId, "bitebynight");
        var forsakenRank = await GetRankAsync(userId, "forsaken");
        var keys = BuildAchievementKeys(scores, bestTime, bbnRank, forsakenRank);
        var totalAchievementExperience = keys
            .Select(AchievementCatalog.Get)
            .Where(a => a != null)
            .Sum(a => a!.Experience);

        var experience = scores.Count * BaseGameExperience + totalAchievementExperience;
        var level = CalculateLevel(experience);
        var totalMs = scores.Sum(s => s.TimeMs);

        await _db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "UserStats" (
                "Id", "UserId", "GamesPlayed", "Wins", "TotalPlayTimeMs",
                "Experience", "Level", "FavoriteGenerator", "UpdatedAt"
            )
            SELECT
                COALESCE((SELECT MAX("Id") FROM "UserStats"), 0) + 1,
                {userId}, {scores.Count}, {scores.Count}, {totalMs},
                {experience}, {level}, {favorite}, now()
            WHERE NOT EXISTS (
                SELECT 1 FROM "UserStats" WHERE "UserId" = {userId}
            );
            """);

        await _db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE "UserStats"
            SET "GamesPlayed" = {scores.Count},
                "Wins" = {scores.Count},
                "TotalPlayTimeMs" = {totalMs},
                "Experience" = GREATEST("Experience", {experience}),
                "Level" = GREATEST("Level", {level}),
                "FavoriteGenerator" = {favorite},
                "UpdatedAt" = now()
            WHERE "UserId" = {userId};
            """);

        var unlocked = new List<AchievementDto>();

        foreach (var key in keys)
        {
            var def = AchievementCatalog.Get(key);
            if (def == null) continue;

            var rows = await _db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO "UserAchievements" (
                    "Id", "UserId", "Key", "Title", "Description",
                    "Icon", "Experience", "UnlockedAt"
                )
                SELECT
                    COALESCE((SELECT MAX("Id") FROM "UserAchievements"), 0) + 1,
                    {userId}, {def.Key}, {def.Title}, {def.Description},
                    {def.Icon}, {def.Experience}, now()
                WHERE NOT EXISTS (
                    SELECT 1
                    FROM "UserAchievements"
                    WHERE "UserId" = {userId} AND "Key" = {def.Key}
                );
                """);

            if (rows > 0)
            {
                unlocked.Add(new AchievementDto(
                    def.Key,
                    def.Title,
                    def.Description,
                    def.Icon,
                    def.Experience,
                    DateTime.UtcNow));
            }
        }

        return unlocked;
    }

    private static List<string> BuildAchievementKeys(
        List<ScoreLite> scores,
        long bestTime,
        int? bbnRank,
        int? forsakenRank)
    {
        var keys = new List<string> { "first_game" };

        if (scores.Count >= 5) keys.Add("five_games");
        if (scores.Count >= 20) keys.Add("twenty_games");
        if (scores.Any(s => s.Generator == "bitebynight")) keys.Add("first_bbn");
        if (scores.Any(s => s.Generator == "forsaken")) keys.Add("first_forsaken");
        if (bbnRank is > 0 and <= 10) keys.Add("bbn_top_10");
        if (forsakenRank is > 0 and <= 10) keys.Add("forsaken_top_10");
        if (bestTime <= 30_000) keys.Add("sub_30");
        if (bestTime <= 10_000) keys.Add("sub_10");
        if (bestTime <= 5_000) keys.Add("sub_5");

        var experienceBeforeLevelAchievements = scores.Count * BaseGameExperience +
            keys.Select(AchievementCatalog.Get).Where(a => a != null).Sum(a => a!.Experience);
        var level = CalculateLevel(experienceBeforeLevelAchievements);

        if (level >= 5) keys.Add("level_5");
        if (level >= 10) keys.Add("level_10");

        return keys.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
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
    private record ScoreLite(string Generator, long TimeMs);
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
