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
            return StatusCode(500, new { error = "Save failed" });
        }

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

    public record SubmitRequest(long TimeMs, string Generator);
}
