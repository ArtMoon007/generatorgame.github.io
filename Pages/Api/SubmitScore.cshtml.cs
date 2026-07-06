using GeneratorGame.Data;
using GeneratorGame.Data.Models;
using GeneratorGame.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace GeneratorGame.Pages.Api;

[IgnoreAntiforgeryToken]
public class SubmitScoreModel : PageModel
{
    private static readonly HashSet<string> AllowedGenerators = new(StringComparer.OrdinalIgnoreCase)
    {
        "bitebynight",
        "forsaken"
    };

    private readonly AppDbContext _db;
    private readonly AchievementService _achievements;

    public SubmitScoreModel(AppDbContext db, AchievementService achievements)
    {
        _db = db;
        _achievements = achievements;
    }

    public async Task<IActionResult> OnPostAsync([FromBody] SubmitRequest req)
    {
        if (req == null) return BadRequest(new { error = "Invalid request" });

        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();

        var generator = string.IsNullOrWhiteSpace(req.Generator)
            ? "bitebynight"
            : req.Generator.Trim().ToLowerInvariant();

        if (!AllowedGenerators.Contains(generator))
            return BadRequest(new { error = "Invalid generator" });

        if (!IsValidTime(generator, req.TimeMs))
            return BadRequest(new { error = "Suspicious time" });

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId.Value);
        if (user == null) return Unauthorized();

        var previousBest = await _db.Scores
            .Where(s => s.UserId == userId.Value && s.Generator == generator)
            .MinAsync(s => (long?)s.TimeMs);

        _db.Scores.Add(new Score
        {
            UserId = userId.Value,
            TimeMs = req.TimeMs,
            Generator = generator
        });

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            return StatusCode(500, new { error = "Score save failed" });
        }

        try
        {
            var rank = await GetRankAsync(userId.Value, generator);
            var rankNotification = BuildRankNotification(rank, previousBest, req.TimeMs);
            var achievementResult = await _achievements.RecordGameAsync(userId.Value, generator, req.TimeMs, rank);
            await _db.SaveChangesAsync();

            return new JsonResult(new
            {
                ok = true,
                userId = userId.Value,
                generator,
                timeMs = req.TimeMs,
                level = achievementResult.Level,
                experience = achievementResult.Experience,
                nextLevelExperience = achievementResult.NextLevelExperience,
                newAchievements = achievementResult.NewAchievements,
                rank,
                rankNotification
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ACHIEVEMENT WARNING: {ex.GetType().Name}: {ex.Message}");

            return new JsonResult(new
            {
                ok = true,
                userId = userId.Value,
                generator,
                timeMs = req.TimeMs,
                achievementWarning = true,
                newAchievements = Array.Empty<object>(),
                rankNotification = (object?)null
            });
        }
    }

    private static bool IsValidTime(string generator, long timeMs)
    {
        if (timeMs <= 0) return false;
        if (timeMs > 600_000) return false;

        var minTime = generator switch
        {
            "bitebynight" => 500,
            "forsaken" => 1_500,
            _ => 500
        };

        return timeMs >= minTime;
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

    private static RankNotification? BuildRankNotification(int? rank, long? previousBest, long timeMs)
    {
        if (rank == null || rank > 100) return null;
        if (previousBest != null && timeMs >= previousBest.Value) return null;

        var label = rank.Value switch
        {
            1 => "топ 1",
            2 => "топ 2",
            3 => "топ 3",
            4 => "топ 4",
            5 => "топ 5",
            <= 10 => "топ 10",
            _ => "топ 100"
        };

        return new RankNotification(
            rank.Value,
            "Поздравляю!",
            $"Ты попал в {label}",
            $"+ место #{rank.Value}");
    }

    public record SubmitRequest(long TimeMs, string Generator);
    public record RankNotification(int Rank, string Title, string Description, string Meta);
}
