using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GeneratorGame.Pages.Generators
{
    public class BiteByNightModel : PageModel
    {
        public IActionResult OnGet()
        {
            if (User.Identity?.IsAuthenticated != true)
            {
                return RedirectToPage("/Auth/Login");
            }

            return Page();
        }
    }
}
