using GeneratorGame.Data;
using Microsoft.EntityFrameworkCore;

namespace GeneratorGame.Services;

public class VipService
{
    private readonly AppDbContext _db;

    public VipService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<bool> IsVipAsync(int userId)
    {
        var vipUntil = await _db.Users
            .Where(u => u.Id == userId)
            .Select(u => u.VipUntil)
            .FirstOrDefaultAsync();

        return vipUntil.HasValue && vipUntil.Value > DateTime.UtcNow;
    }

    public static bool IsVip(DateTime? vipUntil) =>
        vipUntil.HasValue && vipUntil.Value > DateTime.UtcNow;
}
