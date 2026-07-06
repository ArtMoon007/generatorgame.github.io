using GeneratorGame.Data;
using GeneratorGame.Data.Models;
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

    public SubmitScoreModel(AppDbContext db)
    {
        _db = db;
    }

    public async Task<IActionResult> OnPostAsync([FromBody] SubmitRequest req)
    {
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

        var lastScore = await _db.Scores
            .Where(s => s.UserId == userId.Value && s.Generator == generator)
            .OrderByDescending(s => s.Id)
            .FirstOrDefaultAsync();

        if (lastScore != null && req.TimeMs == lastScore.TimeMs)
            return new JsonResult(new
            {
                ok = true,
                duplicate = true,
                userId = userId.Value,
                generator,
                timeMs = req.TimeMs
            });

        _db.Scores.Add(new Score
        {
            UserId = userId.Value,
            TimeMs = req.TimeMs,
            Generator = generator
        });

        await _db.SaveChangesAsync();

        return new JsonResult(new
        {
            ok = true,
            userId = userId.Value,
            generator,
            timeMs = req.TimeMs
        });
    }

    private static bool IsValidTime(string generator, long timeMs)
    {
        if (timeMs <= 0) return false;
        if (timeMs > 600_000) return false;

        var minTime = generator switch
        {
            "bitebynight" => 3_000,
            "forsaken" => 2_000,
            _ => 5_000
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

    public record SubmitRequest(long TimeMs, string Generator);
}
