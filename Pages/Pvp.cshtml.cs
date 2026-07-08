using GeneratorGame.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GeneratorGame.Pages;

public class PvpModel : PageModel
{
    private readonly PvpService _pvp;

    public PvpModel(PvpService pvp)
    {
        _pvp = pvp;
    }

    public IReadOnlyList<PvpRatingView> Ratings { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync()
    {
        var userId = GetCurrentUserId();
        if (userId == null) return RedirectToPage("/Auth/Login");

        Ratings = await _pvp.GetRatingsAsync(userId.Value);
        return Page();
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
