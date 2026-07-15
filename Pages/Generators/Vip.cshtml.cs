using GeneratorGame.Data;
using GeneratorGame.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace GeneratorGame.Pages.Generators;

public class VipModel : PageModel
{
    private readonly AppDbContext _db;

    public VipModel(AppDbContext db)
    {
        _db = db;
    }

    public async Task<IActionResult> OnGetAsync()
    {
        if (User.Identity?.IsAuthenticated != true)
        {
            return Page();
        }

        var userId = HttpContext.Session.GetInt32("UserId");
        if (userId == null) return Page();

        var vipUntil = await _db.Users
            .Where(u => u.Id == userId.Value)
            .Select(u => u.VipUntil)
            .FirstOrDefaultAsync();

        if (!VipService.IsVip(vipUntil))
        {
            return RedirectToPage("/Vip");
        }

        return Page();
    }
}
