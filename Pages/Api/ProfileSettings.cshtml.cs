using System.Text.RegularExpressions;
using GeneratorGame.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace GeneratorGame.Pages.Api;

[IgnoreAntiforgeryToken]
public class ProfileSettingsModel : PageModel
{
    private static readonly Regex UsernameRegex = new("^[a-zA-Z0-9_]{3,20}$", RegexOptions.Compiled);
    private readonly AppDbContext _db;

    public ProfileSettingsModel(AppDbContext db)
    {
        _db = db;
    }

    public async Task<IActionResult> OnPostUsernameAsync([FromBody] UsernameRequest? request)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();

        var username = (request?.Username ?? string.Empty).Trim();
        if (!UsernameRegex.IsMatch(username))
        {
            return BadRequest(new { error = "Ник должен быть 3-20 символов: латиница, цифры или _" });
        }

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId.Value);
        if (user == null) return Unauthorized();

        if (string.Equals(user.Username, username, StringComparison.OrdinalIgnoreCase))
        {
            return new JsonResult(new { ok = true, username = user.Username });
        }

        if (user.UsernameChangedAt.HasValue && user.UsernameChangedAt.Value.AddDays(30) > DateTime.UtcNow)
        {
            return BadRequest(new
            {
                error = "Ник можно менять один раз в месяц",
                nextChangeAt = user.UsernameChangedAt.Value.AddDays(30)
            });
        }

        var normalizedUsername = username.ToLower();
        var busy = await _db.Users.AnyAsync(u => u.Id != user.Id && u.Username.ToLower() == normalizedUsername);
        if (busy)
        {
            return BadRequest(new { error = "Этот ник уже занят" });
        }

        user.Username = username;
        user.UsernameChangedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        HttpContext.Session.SetString("UserName", username);

        return new JsonResult(new
        {
            ok = true,
            username,
            nextChangeAt = user.UsernameChangedAt.Value.AddDays(30)
        });
    }

    public async Task<IActionResult> OnPostCosmeticsAsync([FromBody] CosmeticsRequest? request)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();

        var stat = await _db.UserStats.FirstOrDefaultAsync(s => s.UserId == userId.Value);
        if (stat == null) return BadRequest(new { error = "Профиль еще не готов" });

        stat.RainbowNameEnabled = stat.RainbowNameUnlocked && (request?.RainbowName ?? false);
        stat.DiamondEmojiEnabled = stat.DiamondEmojiUnlocked && (request?.DiamondEmoji ?? false);
        stat.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return new JsonResult(new
        {
            ok = true,
            stat.RainbowNameEnabled,
            stat.DiamondEmojiEnabled
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

    public record UsernameRequest(string? Username);
    public record CosmeticsRequest(bool RainbowName, bool DiamondEmoji);
}
