using GeneratorGame.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GeneratorGame.Pages.Api;

[IgnoreAntiforgeryToken]
public class OnlineModel : PageModel
{
    private readonly OnlineTracker _online;
    private readonly VisitCounterService _visits;

    public OnlineModel(OnlineTracker online, VisitCounterService visits)
    {
        _online = online;
        _visits = visits;
    }

    public async Task<IActionResult> OnPostAsync([FromBody] OnlineRequest request)
    {
        var clientId = string.IsNullOrWhiteSpace(request.ClientId)
            ? HttpContext.TraceIdentifier
            : request.ClientId.Trim();

        var snapshot = _online.Touch(clientId);
        var visits = await _visits.RegisterVisitAsync(request.CountVisit);
        return new JsonResult(new { online = snapshot.Online, visits });
    }

    public async Task<IActionResult> OnGetAsync()
    {
        var snapshot = _online.Snapshot();
        var visits = await _visits.GetVisitsAsync();
        return new JsonResult(new { online = snapshot.Online, visits });
    }

    public record OnlineRequest(string ClientId, bool CountVisit = false);
}
