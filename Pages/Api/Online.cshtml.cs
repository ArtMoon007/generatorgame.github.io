using GeneratorGame.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GeneratorGame.Pages.Api;

[IgnoreAntiforgeryToken]
public class OnlineModel : PageModel
{
    private readonly VisitCounterService _visits;

    public OnlineModel(VisitCounterService visits)
    {
        _visits = visits;
    }

    public async Task<IActionResult> OnPostAsync([FromBody] OnlineRequest request)
    {
        var clientId = string.IsNullOrWhiteSpace(request.ClientId)
            ? HttpContext.TraceIdentifier
            : request.ClientId.Trim();

        var snapshot = await _visits.RegisterVisitAsync(clientId, request.CountVisit);
        return new JsonResult(new { online = snapshot.Online, visits = snapshot.Visits });
    }

    public async Task<IActionResult> OnGetAsync()
    {
        var snapshot = await _visits.GetSnapshotAsync();
        return new JsonResult(new { online = snapshot.Online, visits = snapshot.Visits });
    }

    public record OnlineRequest(string ClientId, bool CountVisit = false);
}
