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
    public IReadOnlyList<PvpTopView> TopPlayers { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync()
    {
        var userId = GetCurrentUserId();
        Ratings = userId == null
            ?
            [
                new PvpRatingView("bitebynight", "Bite by Night", 0, "Дерево", 0, "🪵", "wood", 0, 0),
                new PvpRatingView("forsaken", "Forsaken", 0, "Дерево", 0, "🪵", "wood", 0, 0)
            ]
            : await _pvp.GetRatingsAsync(userId.Value);
        TopPlayers = await _pvp.GetTopAsync();
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
