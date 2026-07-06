using GeneratorGame.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GeneratorGame.Pages.Api;

[IgnoreAntiforgeryToken]
public class OnlineModel : PageModel
{
    private readonly OnlineTracker _online;

    public OnlineModel(OnlineTracker online)
    {
        _online = online;
    }

    public IActionResult OnPost([FromBody] OnlineRequest request)
    {
        var clientId = string.IsNullOrWhiteSpace(request.ClientId)
            ? HttpContext.TraceIdentifier
            : request.ClientId.Trim();

        return new JsonResult(new { online = _online.Touch(clientId) });
    }

    public IActionResult OnGet()
    {
        return new JsonResult(new { online = _online.Count() });
    }

    public record OnlineRequest(string ClientId);
}
