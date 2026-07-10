using GeneratorGame.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GeneratorGame.Pages.Api;

[IgnoreAntiforgeryToken]
public class DailyRewardsModel : PageModel
{
    private readonly DailyRewardService _rewards;

    public DailyRewardsModel(DailyRewardService rewards)
    {
        _rewards = rewards;
    }

    public async Task<IActionResult> OnGetAsync()
    {
        var userId = GetCurrentUserId();
        if (userId == null) return new JsonResult(new { loggedIn = false });

        var view = await _rewards.GetViewAsync(userId.Value);
        return new JsonResult(new { loggedIn = true, reward = view });
    }

    public async Task<IActionResult> OnPostClaimAsync()
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();

        var view = await _rewards.ClaimAsync(userId.Value);
        return new JsonResult(new { ok = true, reward = view });
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
