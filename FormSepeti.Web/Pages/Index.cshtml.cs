using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Security.Claims;

namespace FormSepeti.Web.Pages
{
    public class IndexModel : PageModel
    {
        public bool IsAuthenticated { get; private set; }
        public string UserName { get; private set; } = "";
        public string SpreadsheetUrl { get; set; } = ""; // <-- Initialized here

        public void OnGet()
        {
            IsAuthenticated = User?.Identity?.IsAuthenticated ?? false;
            if (IsAuthenticated)
            {
                UserName = User.FindFirst(ClaimTypes.Name)?.Value ?? User.Identity?.Name ?? "";
            }

            // Example assignment, replace with your logic
            SpreadsheetUrl = "https://docs.google.com/spreadsheets/...";
        }
    }
}
