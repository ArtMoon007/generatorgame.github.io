using GeneratorGame.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GeneratorGame.Pages.Api;

[IgnoreAntiforgeryToken]
public class PvpModel : PageModel
{
    private readonly PvpService _pvp;

    public PvpModel(PvpService pvp)
    {
        _pvp = pvp;
    }

    public async Task<IActionResult> OnGetStatusAsync(string? generator = null)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();

        var ratings = await _pvp.GetRatingsAsync(userId.Value);
        var match = await _pvp.GetActiveMatchViewAsync(userId.Value, generator);
        var queueCount = string.IsNullOrWhiteSpace(generator)
            ? 0
            : await _pvp.GetQueueCountAsync(userId.Value, NormalizeRequestGenerator(generator));

        return new JsonResult(new
        {
            ok = true,
            ratings,
            match,
            queueCount
        });
    }

    public async Task<IActionResult> OnPostSearchAsync([FromBody] PvpGeneratorRequest request)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();

        var generator = NormalizeRequestGenerator(request.Generator);
        var result = await _pvp.SearchAsync(userId.Value, generator);
        var match = await _pvp.GetActiveMatchViewAsync(userId.Value, generator);

        return new JsonResult(new
        {
            ok = true,
            result,
            match
        });
    }

    public async Task<IActionResult> OnPostCancelAsync([FromBody] PvpGeneratorRequest request)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();

        await _pvp.CancelSearchAsync(userId.Value, NormalizeRequestGenerator(request.Generator));
        return new JsonResult(new { ok = true });
    }

    public async Task<IActionResult> OnGetInviteAsync()
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();

        var invite = await _pvp.GetPendingInviteAsync(userId.Value);
        return new JsonResult(new { ok = true, invite });
    }

    public async Task<IActionResult> OnPostInviteAsync([FromBody] PvpInviteRequest request)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();

        var result = await _pvp.SendInviteAsync(
            userId.Value,
            request.Nickname ?? string.Empty,
            NormalizeRequestGenerator(request.Generator));

        return new JsonResult(new
        {
            ok = result.Ok,
            result.Message,
            result.InviteId
        });
    }

    public async Task<IActionResult> OnPostRespondInviteAsync([FromBody] PvpInviteResponseRequest request)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();

        var result = await _pvp.RespondInviteAsync(userId.Value, request.InviteId, request.Accept);
        if (result == null) return NotFound();

        return new JsonResult(new
        {
            ok = true,
            result.Accepted,
            result.MatchId,
            result.Match
        });
    }

    public async Task<IActionResult> OnPostReadyAsync([FromBody] PvpMatchRequest request)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();

        var match = await _pvp.ReadyAsync(userId.Value, request.MatchId);
        if (match == null) return NotFound();

        return new JsonResult(new { ok = true, match });
    }

    public async Task<IActionResult> OnPostSubmitRoundAsync([FromBody] PvpRoundRequest request)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();
        if (request.TimeMs <= 0 || request.TimeMs > 600_000) return BadRequest(new { error = "Invalid time" });

        var match = await _pvp.SubmitRoundAsync(userId.Value, request.MatchId, request.TimeMs);
        if (match == null) return NotFound();

        return new JsonResult(new { ok = true, match });
    }

    public async Task<IActionResult> OnPostForfeitAsync([FromBody] PvpMatchRequest request)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();

        var match = await _pvp.ForfeitAsync(userId.Value, request.MatchId);
        if (match == null) return NotFound();

        return new JsonResult(new { ok = true, match });
    }

    private static string NormalizeRequestGenerator(string? generator)
    {
        var normalized = string.IsNullOrWhiteSpace(generator)
            ? "bitebynight"
            : generator.Trim().ToLowerInvariant();

        return PvpService.IsAllowedGenerator(normalized) ? normalized : "bitebynight";
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

    public record PvpGeneratorRequest(string? Generator);
    public record PvpInviteRequest(string? Generator, string? Nickname);
    public record PvpInviteResponseRequest(int InviteId, bool Accept);
    public record PvpMatchRequest(int MatchId);
    public record PvpRoundRequest(int MatchId, long TimeMs);
}
