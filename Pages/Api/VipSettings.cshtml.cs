using GeneratorGame.Data;
using GeneratorGame.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace GeneratorGame.Pages.Api;

[IgnoreAntiforgeryToken]
public class VipSettingsModel : PageModel
{
    private readonly AppDbContext _db;

    public VipSettingsModel(AppDbContext db)
    {
        _db = db;
    }

    public async Task<IActionResult> OnPostAsync([FromBody] VipSettingsRequest request)
    {
        var userId = HttpContext.Session.GetInt32("UserId");
        if (userId == null) return Unauthorized();

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId.Value);
        if (user == null) return Unauthorized();
        if (!VipService.IsVip(user.VipUntil)) return Forbid();

        user.HideStatsFromOthers = request.HideStats;
        await _db.SaveChangesAsync();

        return new JsonResult(new { ok = true, user.HideStatsFromOthers });
    }

    public record VipSettingsRequest(bool HideStats);
}
