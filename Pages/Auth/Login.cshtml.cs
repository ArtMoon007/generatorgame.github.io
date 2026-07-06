using System.Security.Claims;
using GeneratorGame.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace GeneratorGame.Pages.Auth;

[IgnoreAntiforgeryToken]
public class LoginModel : PageModel
{
    private readonly AppDbContext _db;
    public string? ErrorMessage { get; private set; }

    public LoginModel(AppDbContext db) => _db = db;

    public IActionResult OnGet()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToPage("/Index");
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(string login, string password, bool rememberMe = true)
    {
        login = (login ?? string.Empty).Trim();
        password ??= string.Empty;
        var normalizedLogin = login.ToLowerInvariant();

        var user = await _db.Users.FirstOrDefaultAsync(u =>
            u.Username.ToLower() == normalizedLogin ||
            (u.Email != null && u.Email.ToLower() == normalizedLogin));

        if (user == null || string.IsNullOrEmpty(user.PasswordHash))
        {
            ErrorMessage = "Неверный логин или пароль";
            return Page();
        }

        if (!BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
        {
            ErrorMessage = "Неверный логин или пароль";
            return Page();
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.Username)
        };

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);
        var authProperties = new AuthenticationProperties
        {
            IsPersistent = rememberMe,
            ExpiresUtc = rememberMe ? DateTimeOffset.UtcNow.AddDays(30) : DateTimeOffset.UtcNow.AddHours(2)
        };

        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal, authProperties);

        HttpContext.Session.SetInt32("UserId", user.Id);
        HttpContext.Session.SetString("UserName", user.Username);

        return RedirectToPage("/Generators/BiteByNight");
    }

    private void SetSession(int id, string name, string? avatar)
    {
        HttpContext.Session.SetInt32("UserId", id);
        HttpContext.Session.SetString("UserName", name);
        if (avatar != null) HttpContext.Session.SetString("UserAvatar", avatar);
    }
}
