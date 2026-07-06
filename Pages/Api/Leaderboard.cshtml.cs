using GeneratorGame.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace GeneratorGame.Pages.Api;

public class LeaderboardModel : PageModel
{
    private readonly AppDbContext _db;
    public LeaderboardModel(AppDbContext db) => _db = db;

    public async Task<IActionResult> OnGetAsync(string generator = "bitebynight")
    {
        // Лучший результат каждого игрока в этом генераторе, топ 100
        var top = await _db.Scores
            .Where(s => s.Generator == generator)
            .GroupBy(s => s.UserId)
            .Select(g => new
            {
                UserId = g.Key,
                BestMs = g.Min(s => s.TimeMs)
            })
            .OrderBy(x => x.BestMs)
            .Take(100)
            .Join(_db.Users,
                s => s.UserId,
                u => u.Id,
                (s, u) => new
                {
                    u.Id,
                    u.Username,
                    u.RobloxUsername,
                    u.RobloxId,
                    u.RobloxAvatarUrl,
                    TimeMs = s.BestMs,
                    TimeFormatted = $"{s.BestMs / 60000:D2}:{(s.BestMs % 60000) / 1000:D2}.{s.BestMs % 1000:D3}"
                })
            .ToListAsync();

        return new JsonResult(top);
    }
}
