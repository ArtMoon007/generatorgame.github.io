using System.Security.Claims;
using GeneratorGame.Data;
using GeneratorGame.Data.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace GeneratorGame.Pages.Auth;

public class RegisterModel : PageModel
{
    private readonly AppDbContext _db;

    public RegisterModel(AppDbContext db)
    {
        _db = db;
    }

    [BindProperty] public string Username { get; set; } = "";
    [BindProperty] public string Password { get; set; } = "";
    [BindProperty] public string Confirm { get; set; } = "";

    public string? ErrorMessage { get; set; }

    public IActionResult OnGet()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToPage("/Index");
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (string.IsNullOrWhiteSpace(Username) ||
            string.IsNullOrWhiteSpace(Password))
        {
            ErrorMessage = "Заполни все поля";
            return Page();
        }

        if (Password != Confirm)
        {
            ErrorMessage = "Пароли не совпадают";
            return Page();
        }

        if (Password.Length < 6)
        {
            ErrorMessage = "Пароль слишком короткий";
            return Page();
        }

        // проверка уникальности
        var exists = await _db.Users.AnyAsync(u => u.Username == Username);
        if (exists)
        {
            ErrorMessage = "Такой пользователь уже существует";
            return Page();
        }

        // hash пароля
        var hash = BCrypt.Net.BCrypt.HashPassword(Password);

        var user = new User
        {
            Username = Username,
            PasswordHash = hash
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.Username)
        };

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);
        var authProperties = new AuthenticationProperties
        {
            IsPersistent = true,
            ExpiresUtc = DateTimeOffset.UtcNow.AddDays(30)
        };

        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal, authProperties);

        HttpContext.Session.SetInt32("UserId", user.Id);
        HttpContext.Session.SetString("UserName", user.Username);

        return RedirectToPage("/Index");
    }
}