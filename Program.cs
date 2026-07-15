using System.Security.Claims;
using GeneratorGame.Data;
using GeneratorGame.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

var port = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrWhiteSpace(port))
{
    builder.WebHost.UseUrls($"http://0.0.0.0:{port}");
}

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.SetMinimumLevel(LogLevel.Warning);

Console.WriteLine(string.IsNullOrWhiteSpace(port)
    ? "APP PORT: local/default"
    : $"APP PORT: {port}");

builder.Services.AddRazorPages();
builder.Services.AddSignalR();

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
builder.Services.AddScoped<AchievementService>();
builder.Services.AddScoped<VisitCounterService>();
builder.Services.AddScoped<VipService>();
builder.Services.AddScoped<PvpService>();
builder.Services.AddScoped<DailyRewardService>();
builder.Services.AddScoped<EnotPaymentService>();
builder.Services.AddSingleton<KillerGameService>();

var app = builder.Build();

// Apply migrations
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    Console.WriteLine("DATABASE MIGRATION START");
    db.Database.Migrate();
    Console.WriteLine("DATABASE MIGRATION DONE");

    Console.WriteLine("DATABASE SCHEMA INIT START");
    AchievementDatabaseInitializer.EnsureSchemaAsync(db).GetAwaiter().GetResult();
    ProfileDatabaseInitializer.EnsureSchemaAsync(db).GetAwaiter().GetResult();
    PvpDatabaseInitializer.EnsureSchemaAsync(db).GetAwaiter().GetResult();

    var visits = scope.ServiceProvider.GetRequiredService<VisitCounterService>();
    visits.EnsureSchemaAsync().GetAwaiter().GetResult();

    var rewards = scope.ServiceProvider.GetRequiredService<DailyRewardService>();
    rewards.EnsureSchemaAsync().GetAwaiter().GetResult();

    var enot = scope.ServiceProvider.GetRequiredService<EnotPaymentService>();
    enot.EnsureSchemaAsync().GetAwaiter().GetResult();
    Console.WriteLine("DATABASE SCHEMA INIT DONE");
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
        context.Response.Redirect("/Generators/Forsaken");
        return;
    }

    if (context.Request.Path.StartsWithSegments("/auth/register") &&
        context.User.Identity?.IsAuthenticated == true)
    {
        context.Response.Redirect("/Generators/Forsaken");
        return;
    }

    await next();
});

app.MapRazorPages();
app.MapHub<KillerGameHub>("/hubs/killer-game");

app.Lifetime.ApplicationStarted.Register(() =>
{
    Console.WriteLine("APPLICATION STARTED");

    _ = Task.Run(async () =>
    {
        try
        {
            Console.WriteLine("ACHIEVEMENT BACKFILL START");
            using var scope = app.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var achievements = scope.ServiceProvider.GetRequiredService<AchievementService>();
            await AchievementDatabaseInitializer.BackfillAllAsync(db, achievements);
            Console.WriteLine("ACHIEVEMENT BACKFILL DONE");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ACHIEVEMENT BACKFILL ERROR: {ex.GetType().Name}: {ex.Message}");
        }
    });
});

app.Run();
