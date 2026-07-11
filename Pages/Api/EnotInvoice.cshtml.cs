using GeneratorGame.Data;
using GeneratorGame.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace GeneratorGame.Pages.Api;

[IgnoreAntiforgeryToken]
public class EnotInvoiceModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly EnotPaymentService _payments;

    public EnotInvoiceModel(AppDbContext db, EnotPaymentService payments)
    {
        _db = db;
        _payments = payments;
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var userId = HttpContext.Session.GetInt32("UserId");
        if (userId == null)
        {
            return new UnauthorizedObjectResult(new { error = "Войди в аккаунт" });
        }

        var user = await _db.Users
            .Where(u => u.Id == userId.Value)
            .Select(u => new { u.Id, u.Username, u.Email })
            .FirstOrDefaultAsync();

        if (user == null)
        {
            return new UnauthorizedObjectResult(new { error = "Войди в аккаунт" });
        }

        var invoice = await _payments.CreateVipInvoiceAsync(user.Id, user.Username, user.Email);
        if (!invoice.Ok || string.IsNullOrWhiteSpace(invoice.Url))
        {
            Console.WriteLine($"ENOT INVOICE ERROR: {invoice.Error}");
            return StatusCode(500, new { error = "Не удалось открыть оплату", detail = invoice.Error });
        }

        return new JsonResult(new { ok = true, url = invoice.Url });
    }
}
