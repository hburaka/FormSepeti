using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Security.Claims;
using System.Threading.Tasks;

namespace FormSepeti.Web.Pages
{
    public class IndexModel : PageModel
    {
        public bool IsAuthenticated { get; private set; }
        public string UserName { get; private set; } = "";

        public void OnGet()
        {
            IsAuthenticated = User?.Identity?.IsAuthenticated ?? false;
            if (IsAuthenticated)
            {
                UserName = User.FindFirst(ClaimTypes.Name)?.Value ?? User.Identity?.Name ?? "";
            }
        }

        public async Task<IActionResult> OnPostLogoutAsync()
        {
            await HttpContext.SignOutAsync("Cookie");
            return RedirectToPage();
        }
    }
}
