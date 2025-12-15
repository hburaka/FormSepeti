using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FormSepeti.Web.Pages.Package
{
    public class SuccessModel : PageModel
    {
        public string PackageName { get; private set; } = "Paket";
        public string Amount { get; private set; } = "99 TL";
        public string TransactionId { get; private set; } = "ABC123";

        public void OnGet(int? packageId)
        {
            // gerçek veriler buradan yüklenir
        }
    }
}
