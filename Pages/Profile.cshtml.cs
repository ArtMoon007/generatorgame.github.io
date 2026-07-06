using GeneratorGame.Data;
using GeneratorGame.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace GeneratorGame.Pages;

public class ProfileModel : PageModel
{
    private readonly AppDbContext _db;

    public ProfileModel(AppDbContext db)
    {
        _db = db;
    }

    public ProfileView View { get; private set; } = new();

    public async Task<IActionResult> OnGetAsync()
    {
        var userId = GetCurrentUserId();
        if (userId == null) return RedirectToPage("/Auth/Login");

        var user = await _db.Users
            .Where(u => u.Id == userId.Value)
            .Select(u => new
            {
                u.Id,
                u.Username,
                u.RobloxUsername,
                u.RobloxAvatarUrl,
                u.AvatarUrl,
                Stat = u.Stat
            })
            .FirstOrDefaultAsync();

        if (user == null) return RedirectToPage("/Auth/Login");

        var scores = await _db.Scores
            .Where(s => s.UserId == userId.Value)
            .Select(s => new { s.Generator, s.TimeMs })
            .ToListAsync();

        var achievements = await _db.UserAchievements
            .Where(a => a.UserId == userId.Value)
            .OrderByDescending(a => a.UnlockedAt)
            .Select(a => new AchievementCard(
                a.Key,
                a.Title,
                a.Description,
                a.Icon,
                a.Experience,
                true,
                a.UnlockedAt))
            .ToListAsync();

        var unlockedKeys = achievements.Select(a => a.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var locked = AchievementCatalog.All
            .Where(a => !unlockedKeys.Contains(a.Key))
            .Select(a => new AchievementCard(a.Key, a.Title, a.Description, a.Icon, a.Experience, false, null));

        var favorite = scores
            .GroupBy(s => s.Generator)
            .OrderByDescending(g => g.Count())
            .ThenBy(g => g.Key)
            .Select(g => g.Key)
            .FirstOrDefault() ?? user.Stat?.FavoriteGenerator ?? "bitebynight";

        var best = scores.Count > 0 ? scores.Min(s => s.TimeMs) : 0;
        var average = scores.Count > 0 ? (long)scores.Average(s => s.TimeMs) : 0;
        var totalMs = scores.Sum(s => s.TimeMs);
        var experience = user.Stat?.Experience ?? 0;
        var level = user.Stat?.Level ?? AchievementService.CalculateLevel(experience);
        var nextLevel = AchievementService.ExperienceForNextLevel(level);
        var currentLevel = AchievementService.ExperienceForLevel(level);
        var levelProgress = nextLevel == currentLevel
            ? 100
            : Math.Clamp((int)Math.Round((experience - currentLevel) * 100.0 / (nextLevel - currentLevel)), 0, 100);

        View = new ProfileView
        {
            Username = user.RobloxUsername ?? user.Username,
            AvatarUrl = user.RobloxAvatarUrl ?? user.AvatarUrl,
            Level = level,
            Experience = experience,
            NextLevelExperience = nextLevel,
            LevelProgress = levelProgress,
            BestTime = best > 0 ? FormatTime(best) : "-",
            AverageTime = average > 0 ? FormatTime(average) : "-",
            GamesPlayed = scores.Count,
            FavoriteGenerator = GeneratorName(favorite),
            TotalHours = totalMs / 3_600_000.0,
            Rank = await GetRankAsync(userId.Value, favorite),
            Achievements = achievements.Concat(locked).ToList()
        };

        return Page();
    }

    private async Task<int?> GetRankAsync(int userId, string generator)
    {
        var ranked = await _db.Scores
            .Where(s => s.Generator == generator)
            .GroupBy(s => s.UserId)
            .Select(g => new { UserId = g.Key, BestMs = g.Min(s => s.TimeMs) })
            .OrderBy(x => x.BestMs)
            .ToListAsync();

        var index = ranked.FindIndex(r => r.UserId == userId);
        return index >= 0 ? index + 1 : null;
    }

    private int? GetCurrentUserId()
    {
        var userId = HttpContext.Session.GetInt32("UserId");
        if (userId != null) return userId;

        var userClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
        if (userClaim != null && int.TryParse(userClaim.Value, out var claimId))
        {
            HttpContext.Session.SetInt32("UserId", claimId);
            return claimId;
        }

        return null;
    }

    private static string FormatTime(long ms) =>
        $"{ms / 60000:D2}:{(ms % 60000) / 1000:D2}.{ms % 1000:D3}";

    private static string GeneratorName(string generator) => generator switch
    {
        "forsaken" => "Forsaken",
        "bitebynight" => "Bite by Night",
        _ => generator
    };
}

public class ProfileView
{
    public string Username { get; set; } = "";
    public string? AvatarUrl { get; set; }
    public int Level { get; set; } = 1;
    public int Experience { get; set; }
    public int NextLevelExperience { get; set; }
    public int LevelProgress { get; set; }
    public string BestTime { get; set; } = "-";
    public string AverageTime { get; set; } = "-";
    public int GamesPlayed { get; set; }
    public string FavoriteGenerator { get; set; } = "Bite by Night";
    public double TotalHours { get; set; }
    public int? Rank { get; set; }
    public List<AchievementCard> Achievements { get; set; } = new();
}

public record AchievementCard(
    string Key,
    string Title,
    string Description,
    string Icon,
    int Experience,
    bool Unlocked,
    DateTime? UnlockedAt);
