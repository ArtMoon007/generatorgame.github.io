using System.Security.Claims;
using GeneratorGame.Data;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);


builder.Services.AddRazorPages();

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/auth/login";
        options.AccessDeniedPath = "/auth/login";
        options.ExpireTimeSpan = TimeSpan.FromDays(30);
        options.SlidingExpiration = true;
    });

builder.Services.AddAuthorization();

// DB
var databaseUrl = Environment.GetEnvironmentVariable("DATABASE_URL");

builder.Services.AddDbContext<AppDbContext>(opt =>
{
    if (!string.IsNullOrWhiteSpace(databaseUrl))
    {
        if (databaseUrl.StartsWith("postgres://") || databaseUrl.StartsWith("postgresql://"))
        {
            var uri = new Uri(databaseUrl);
            var userInfo = uri.UserInfo.Split(':');

            var username = Uri.UnescapeDataString(userInfo[0]);
            var password = Uri.UnescapeDataString(userInfo[1]);
            var host = uri.Host;
            var port = uri.Port;
            var database = uri.AbsolutePath.TrimStart('/');

            var connectionString =
                $"Host={host};Port={port};Database={database};Username={username};Password={password};SSL Mode=Require;Trust Server Certificate=true";

            opt.UseNpgsql(connectionString);
        }
        else
        {
            opt.UseNpgsql(databaseUrl);
        }
    }
    else
    {
        opt.UseSqlite("Data Source=generator.db");
    }
});

Console.WriteLine(string.IsNullOrWhiteSpace(databaseUrl)
    ? "DATABASE: SQLite fallback"
    : "DATABASE: PostgreSQL DATABASE_URL found");


// Session
builder.Services.AddDistributedMemoryCache();

builder.Services.AddSession(opt =>
{
    opt.IdleTimeout = TimeSpan.FromDays(7);
    opt.Cookie.HttpOnly = true;
    opt.Cookie.IsEssential = true;
});

builder.Services.AddHttpClient();

var app = builder.Build();

// Apply migrations
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
}

app.UseStaticFiles();

app.UseRouting();

app.UseSession();

app.UseAuthentication();

app.UseAuthorization();

app.Use(async (context, next) =>
{
    if (context.User.Identity?.IsAuthenticated == true && context.Session.GetInt32("UserId") is null)
    {
        var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        var userName = context.User.Identity.Name;

        if (int.TryParse(userId, out var parsedId))
        {
            context.Session.SetInt32("UserId", parsedId);
            context.Session.SetString("UserName", userName ?? string.Empty);
        }
    }

    if (context.Request.Path.StartsWithSegments("/auth/login") &&
        context.User.Identity?.IsAuthenticated == true)
    {
        context.Response.Redirect("/", true);
        return;
    }

    if (context.Request.Path.StartsWithSegments("/auth/register") &&
        context.User.Identity?.IsAuthenticated == true)
    {
        context.Response.Redirect("/", true);
        return;
    }

    await next();
});

app.MapRazorPages();

app.Run();