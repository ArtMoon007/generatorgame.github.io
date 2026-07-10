using GeneratorGame.Data;
using GeneratorGame.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace GeneratorGame.Pages.Api;

[IgnoreAntiforgeryToken]
public class PlayerLevelModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly AchievementService _achievements;

    public PlayerLevelModel(AppDbContext db, AchievementService achievements)
    {
        _db = db;
        _achievements = achievements;
    }

    public async Task<IActionResult> OnGetAsync()
    {
        var userId = GetCurrentUserId();
        if (userId == null) return new JsonResult(new { loggedIn = false });

        try
        {
            await _achievements.BackfillAsync(userId.Value);
            await _db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"PLAYER LEVEL WARNING: {ex.GetType().Name}: {ex.Message}");
        }

        var user = await _db.Users
            .Where(u => u.Id == userId.Value)
            .Select(u => new { u.Username, u.RobloxUsername })
            .FirstOrDefaultAsync();

        var stat = await _db.UserStats
            .Where(s => s.UserId == userId.Value)
            .Select(s => new { s.Experience, s.Level, s.Diamons })
            .FirstOrDefaultAsync();

        var experience = stat?.Experience ?? 0;
        var level = stat?.Level ?? AchievementService.CalculateLevel(experience);
        var currentLevelXp = AchievementService.ExperienceForLevel(level);
        var nextLevelXp = AchievementService.ExperienceForNextLevel(level);
        var progress = nextLevelXp == currentLevelXp
            ? 100
            : Math.Clamp((int)Math.Round((experience - currentLevelXp) * 100.0 / (nextLevelXp - currentLevelXp)), 0, 100);
        var left = Math.Max(0, nextLevelXp - experience);

        return new JsonResult(new
        {
            loggedIn = true,
            username = user?.RobloxUsername ?? user?.Username ?? "Player",
            level,
            experience,
            diamons = stat?.Diamons ?? 0,
            nextLevelExperience = nextLevelXp,
            experienceToNextLevel = left,
            progress,
            tip = BuildTip(experience, level),
            tips = Tips
        });
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

    private static string BuildTip(int experience, int level)
    {
        return Tips[Math.Abs(experience + level) % Tips.Length];
    }

    private static readonly string[] Tips =
    [
        "Играй в PVP: там опыт идет в 2 раза быстрее.",
        "Делай квесты: за достижения дают больше всего XP.",
        "Пробуй оба генератора: разные квесты быстрее поднимают уровень.",
        "Залетай в топы: топ-ачивки дают крупный бонус.",
        "Играй каждый день: попытки быстро превращаются в уровни."
    ];
}
