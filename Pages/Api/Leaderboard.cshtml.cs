using GeneratorGame.Data;
using GeneratorGame.Services;
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
        generator = string.IsNullOrWhiteSpace(generator)
            ? "bitebynight"
            : generator.Trim().ToLowerInvariant();

        var now = DateTime.UtcNow;

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
                    u.VipUntil,
                    u.HideStatsFromOthers,
                    TimeMs = s.BestMs
                })
            .Where(x => generator != "vip" || (x.VipUntil != null && x.VipUntil > now))
            .ToListAsync();

        var currentUserId = GetCurrentUserId();
        var userIds = top.Select(x => x.Id).ToList();
        var cosmetics = await _db.UserStats
            .Where(s => userIds.Contains(s.UserId))
            .Select(s => new
            {
                s.UserId,
                s.RainbowNameEnabled,
                s.DiamondEmojiEnabled
            })
            .ToDictionaryAsync(s => s.UserId);

        return new JsonResult(top.Select(x =>
        {
            cosmetics.TryGetValue(x.Id, out var cosmetic);
            return new
            {
                x.Id,
                Username = x.HideStatsFromOthers && currentUserId != x.Id ? "VIP Player" : x.Username,
                RobloxUsername = x.HideStatsFromOthers && currentUserId != x.Id ? null : x.RobloxUsername,
                RobloxId = x.HideStatsFromOthers && currentUserId != x.Id ? null : x.RobloxId,
                RobloxAvatarUrl = x.HideStatsFromOthers && currentUserId != x.Id ? null : x.RobloxAvatarUrl,
                isVip = VipService.IsVip(x.VipUntil),
                rainbowName = cosmetic?.RainbowNameEnabled ?? false,
                diamondEmoji = cosmetic?.DiamondEmojiEnabled ?? false,
                statsHidden = x.HideStatsFromOthers && currentUserId != x.Id,
                timeMs = x.TimeMs,
                timeFormatted = $"{x.TimeMs / 60000:D2}:{(x.TimeMs % 60000) / 1000:D2}.{x.TimeMs % 1000:D3}"
            };
        }));
    }

    private int? GetCurrentUserId()
    {
        var userId = HttpContext.Session.GetInt32("UserId");
        if (userId != null) return userId;

        var userClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
        return userClaim != null && int.TryParse(userClaim.Value, out var claimId) ? claimId : null;
    }
}
