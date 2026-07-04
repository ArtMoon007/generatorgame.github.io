using GeneratorGame.Data;
using GeneratorGame.Data.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace GeneratorGame.Pages.Auth;

public class RobloxCallbackModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly IConfiguration _config;
    private readonly IHttpClientFactory _factory;

    public RobloxCallbackModel(
        AppDbContext db,
        IConfiguration config,
        IHttpClientFactory factory)
    {
        _db = db;
        _config = config;
        _factory = factory;
    }

    public async Task<IActionResult> OnGetAsync(string code, string state)
    {
        // =========================
        // 1. CSRF CHECK (STATE)
        // =========================
        var savedState = HttpContext.Session.GetString("OAuthState");
        HttpContext.Session.Remove("OAuthState"); // 🔥 защита от повторного использования

        if (string.IsNullOrEmpty(savedState) || state != savedState)
            return RedirectToPage("/Auth/Login");

        var http = _factory.CreateClient();

        // =========================
        // 2. EXCHANGE CODE -> TOKEN
        // =========================
        var tokenResponse = await http.PostAsync(
            "https://apis.roblox.com/oauth/v1/token",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "authorization_code",
                ["code"] = code,
                ["client_id"] = _config["Roblox:ClientId"]!,
                ["client_secret"] = _config["Roblox:ClientSecret"]!,
                ["redirect_uri"] = _config["Roblox:RedirectUri"]!
            })
        );

        if (!tokenResponse.IsSuccessStatusCode)
            return RedirectToPage("/Auth/Login");

        var tokenJson = JsonDocument.Parse(await tokenResponse.Content.ReadAsStringAsync());
        var accessToken = tokenJson.RootElement.GetProperty("access_token").GetString();

        if (string.IsNullOrEmpty(accessToken))
            return RedirectToPage("/Auth/Login");

        // =========================
        // 3. GET USER INFO
        // =========================
        var req = new HttpRequestMessage(HttpMethod.Get,
            "https://apis.roblox.com/oauth/v1/userinfo");

        req.Headers.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

        var userResp = await http.SendAsync(req);

        if (!userResp.IsSuccessStatusCode)
            return RedirectToPage("/Auth/Login");

        var userJson = JsonDocument.Parse(await userResp.Content.ReadAsStringAsync());

        var robloxId = userJson.RootElement.GetProperty("sub").GetString()!;
        var robloxName = userJson.RootElement.GetProperty("preferred_username").GetString()!;

        // =========================
        // 4. AVATAR
        // =========================
        string? avatarUrl = await GetAvatar(http, robloxId);

        // =========================
        // 5. FIND OR CREATE USER
        // =========================
        var user = await _db.Users.FirstOrDefaultAsync(x => x.RobloxId == robloxId);

        if (user == null)
        {
            // уникальный username
            var username = robloxName;
            var counter = 1;

            while (await _db.Users.AnyAsync(u => u.Username == username))
            {
                username = $"{robloxName}_{counter++}";
            }

            user = new User
            {
                RobloxId = robloxId,
                RobloxUsername = robloxName,
                Username = username,
                AvatarUrl = avatarUrl
            };

            _db.Users.Add(user);
        }
        else
        {
            user.RobloxUsername = robloxName;
            user.AvatarUrl = avatarUrl;
        }

        await _db.SaveChangesAsync();

        // =========================
        // 6. SESSION LOGIN
        // =========================
        HttpContext.Session.SetInt32("UserId", user.Id);
        HttpContext.Session.SetString("UserName", user.Username);

        if (!string.IsNullOrEmpty(avatarUrl))
            HttpContext.Session.SetString("UserAvatar", avatarUrl);

        return RedirectToPage("/Index");
    }

    // =========================
    // AVATAR HELPER
    // =========================
    private async Task<string?> GetAvatar(HttpClient http, string userId)
    {
        try
        {
            var url =
                $"https://thumbnails.roblox.com/v1/users/avatar-headshot" +
                $"?userIds={userId}&size=48x48&format=Png&isCircular=true";

            var json = await http.GetStringAsync(url);

            var doc = JsonDocument.Parse(json);

            return doc.RootElement
                .GetProperty("data")[0]
                .GetProperty("imageUrl")
                .GetString();
        }
        catch
        {
            return null;
        }
    }
}