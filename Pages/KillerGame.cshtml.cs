using GeneratorGame.Data;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace GeneratorGame.Pages;

public class KillerGameModel : PageModel
{
    private readonly AppDbContext _db;

    public KillerGameModel(AppDbContext db) => _db = db;

    public int Level { get; private set; }
    public bool IsLoggedIn { get; private set; }
    public bool IsUnlocked => IsLoggedIn && Level >= 8;

    public async Task OnGetAsync()
    {
        var userId = HttpContext.Session.GetInt32("UserId");
        IsLoggedIn = User.Identity?.IsAuthenticated == true && userId != null;
        if (!IsLoggedIn) return;

        Level = await _db.UserStats
            .Where(x => x.UserId == userId!.Value)
            .Select(x => x.Level)
            .FirstOrDefaultAsync();
        if (Level < 1) Level = 1;
    }
}
