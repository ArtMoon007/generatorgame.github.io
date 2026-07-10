using GeneratorGame.Data;
using GeneratorGame.Services;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace GeneratorGame.Pages;

public class VipModel : PageModel
{
    private readonly AppDbContext _db;

    public VipModel(AppDbContext db)
    {
        _db = db;
    }

    public bool LoggedIn { get; private set; }
    public string? WidgetUrl { get; private set; }
    public DateTime? VipUntil { get; private set; }

    public async Task OnGetAsync()
    {
        var userId = HttpContext.Session.GetInt32("UserId");
        if (userId == null) return;

        var user = await _db.Users
            .Where(u => u.Id == userId.Value)
            .Select(u => new { u.Id, u.Username, u.Email, u.VipUntil })
            .FirstOrDefaultAsync();

        if (user == null) return;

        LoggedIn = true;
        VipUntil = user.VipUntil;
        WidgetUrl = EnotPaymentService.BuildVipWidgetUrl(user.Id, user.Username, user.Email);
    }
}
