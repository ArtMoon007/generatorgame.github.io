using GeneratorGame.Data;
using GeneratorGame.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace GeneratorGame.Pages.Api;

public class PlayerProfileModel : PageModel
{
    private readonly AppDbContext _db;

    public PlayerProfileModel(AppDbContext db)
    {
        _db = db;
    }

    public async Task<IActionResult> OnGetAsync(int userId)
    {
        if (userId <= 0) return BadRequest(new { error = "Invalid user" });

        var user = await _db.Users
            .Where(u => u.Id == userId)
            .Select(u => new
            {
                u.Id,
                u.Username,
                u.RobloxUsername,
                u.RobloxAvatarUrl,
                u.AvatarUrl,
                u.VipUntil,
                u.HideStatsFromOthers
            })
            .FirstOrDefaultAsync();

        if (user == null) return NotFound(new { error = "Player not found" });

        var scores = await _db.Scores
            .Where(s => s.UserId == userId)
            .Select(s => new { s.Generator, s.TimeMs })
            .ToListAsync();

        var achievements = Array.Empty<object>().ToList();
        var achievementsUnlocked = 0;
        var experience = 0;
        var level = 1;

        try
        {
            var stat = await _db.UserStats
                .Where(s => s.UserId == userId)
                .Select(s => new { s.Experience, s.Level })
                .FirstOrDefaultAsync();

            experience = stat?.Experience ?? 0;
            level = stat?.Level ?? AchievementService.CalculateLevel(experience);

            achievements = await _db.UserAchievements
                .Where(a => a.UserId == userId)
                .OrderByDescending(a => a.UnlockedAt)
                .Take(8)
                .Select(a => new
                {
                    a.Title,
                    a.Description,
                    a.Icon,
                    a.Experience,
                    a.UnlockedAt
                } as object)
                .ToListAsync();

            achievementsUnlocked = await _db.UserAchievements.CountAsync(a => a.UserId == userId);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"PLAYER PROFILE STAT WARNING: {ex.GetType().Name}: {ex.Message}");
        }

        var favorite = scores
            .GroupBy(s => s.Generator)
            .OrderByDescending(g => g.Count())
            .ThenBy(g => g.Key)
            .Select(g => g.Key)
            .FirstOrDefault() ?? "bitebynight";

        var best = scores.Count > 0 ? scores.Min(s => s.TimeMs) : 0;
        var average = scores.Count > 0 ? (long)scores.Average(s => s.TimeMs) : 0;
        var isVip = VipService.IsVip(user.VipUntil);
        var statsHidden = isVip && user.HideStatsFromOthers;

        return new JsonResult(new
        {
            id = user.Id,
            username = user.RobloxUsername ?? user.Username,
            avatarUrl = user.RobloxAvatarUrl ?? user.AvatarUrl,
            isVip,
            statsHidden,
            level = statsHidden ? 0 : level,
            experience = statsHidden ? 0 : experience,
            gamesPlayed = statsHidden ? 0 : scores.Count,
            bestTime = statsHidden ? "скрыто" : (best > 0 ? FormatTime(best) : "-"),
            averageTime = statsHidden ? "скрыто" : (average > 0 ? FormatTime(average) : "-"),
            favoriteGenerator = statsHidden ? "скрыто" : GeneratorName(favorite),
            totalHours = statsHidden ? 0 : Math.Round(scores.Sum(s => s.TimeMs) / 3_600_000.0, 2),
            achievementsUnlocked = statsHidden ? 0 : achievementsUnlocked,
            achievementsTotal = AchievementCatalog.All.Count,
            achievements = statsHidden ? new List<object>() : achievements
        });
    }

    private static string FormatTime(long ms) =>
        $"{ms / 60000:D2}:{(ms % 60000) / 1000:D2}.{ms % 1000:D3}";

    private static string GeneratorName(string generator) => generator switch
    {
        "forsaken" => "Forsaken",
        "bitebynight" => "Bite by Night",
        "vip" => "VIP Generator",
        _ => generator
    };
}
