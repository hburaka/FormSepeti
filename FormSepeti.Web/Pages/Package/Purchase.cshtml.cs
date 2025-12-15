using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Threading.Tasks;
using FormSepeti.Services.Interfaces;
using System.Security.Claims; 

namespace FormSepeti.Web.Pages.Package
{
    public class PurchaseModel : PageModel
    {
        private readonly IPackageService _packageService;
        private readonly IUserService _userService;

        public PurchaseModel(IPackageService packageService, IUserService userService)
        {
            _packageService = packageService;
            _userService = userService;
        }

        [BindProperty(SupportsGet = true)] public int PackageId { get; set; }
        public string PackageName { get; private set; } = "Paket";
        public string PackagePrice { get; private set; } = "99 TL / Ay";

        public async Task OnGetAsync(int packageId)
        {
            PackageId = packageId;
            var package = await _packageService.GetPackageByIdAsync(packageId);
            if (package != null)
            {
                PackageName = package.PackageName;
                PackagePrice = package.Price.ToString();
            }
        }

        public async Task<IActionResult> OnPostAsync()
        {
            // placeholder: integrate payment flow using IIyzicoPaymentService in future
            var userId = int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) ? id : 0;
            await _packageService.PurchasePackageAsync(userId, PackageId, "TEST-" + System.Guid.NewGuid(), 0);
            return RedirectToPage("Success", new { packageId = PackageId });
        }
    }
}
