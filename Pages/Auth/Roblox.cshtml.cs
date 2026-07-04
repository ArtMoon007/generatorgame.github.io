using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GeneratorGame.Pages.Auth
{
    public class RobloxModel : PageModel
    {
        private readonly IConfiguration _config;

        public RobloxModel(IConfiguration config)
        {
            _config = config;
        }

        public IActionResult OnGet()
        {
            var clientId = _config["Roblox:ClientId"];
            var redirectUriRaw = _config["Roblox:RedirectUri"];

            if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(redirectUriRaw))
            {
                return Content("Roblox OAuth config missing");
            }

            var state = Guid.NewGuid().ToString("N");
            HttpContext.Session.SetString("OAuthState", state);

            var redirectUri = Uri.EscapeDataString(redirectUriRaw);

            var url =
                "https://apis.roblox.com/oauth/v1/authorize" +
                $"?client_id={clientId}" +
                $"&redirect_uri={redirectUri}" +
                "&scope=openid%20profile" +
                "&response_type=code" +
                $"&state={state}";

            return Redirect(url);
        }
    }
}