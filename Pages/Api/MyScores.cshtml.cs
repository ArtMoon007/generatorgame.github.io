using GeneratorGame.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace GeneratorGame.Pages.Api;

public class MyScoresModel : PageModel
{
    private readonly AppDbContext _db;
    public MyScoresModel(AppDbContext db) => _db = db;

    public async Task<IActionResult> OnGetAsync(string generator = "bitebynight")
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();

        var user = await _db.Users
            .Where(u => u.Id == userId.Value)
            .Select(u => new { u.Username })
            .FirstOrDefaultAsync();

        var scores = await _db.Scores
            .Where(s => s.UserId == userId.Value && s.Generator == generator)
            .OrderBy(s => s.TimeMs)
            .Select(s => new {
                s.TimeMs,
                TimeFormatted = $"{s.TimeMs/60000:D2}:{(s.TimeMs%60000)/1000:D2}.{s.TimeMs%1000:D3}"
            })
            .ToListAsync();

        return new JsonResult(new {
            username = user?.Username ?? "Игрок",
            scores
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
}
