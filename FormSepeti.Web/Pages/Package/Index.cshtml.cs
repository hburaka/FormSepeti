using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Collections.Generic;
using System.Threading.Tasks;
using FormSepeti.Services.Interfaces;
using FormSepeti.Data.Repositories.Interfaces;

namespace FormSepeti.Web.Pages.Package
{
    public partial class IndexModel : PageModel
    {
        private readonly IFormGroupRepository _groupRepo;
        private readonly IPackageService _packageService;

        public IndexModel(IFormGroupRepository groupRepo, IPackageService packageService)
        {
            _groupRepo = groupRepo;
            _packageService = packageService;
        }

        public class PackageView
        {
            public int PackageId { get; set; }
            public string PackageName { get; set; } // <-- Add this property
            public decimal Price { get; set; }
            public string Description { get; set; }
        }

        public List<PackageView> Packages { get; set; } = new List<PackageView>();

        public async Task OnGetAsync()
        {
            // Load groups, then load packages per group using the existing service methods
            var groups = await _groupRepo.GetAllActiveAsync();
            if (groups == null) return;

            foreach (var g in groups)
            {
                var pkgs = await _packageService.GetPackagesByGroupIdAsync(g.GroupId);
                if (pkgs == null) continue;

                foreach (var p in pkgs)
                {
                    // map domain Package entity fields to PackageView; adjust names if your entity differs
                    Packages.Add(new PackageView
                    {
                        PackageId = p.PackageId,
                        PackageName = p.PackageName ?? string.Empty,
                        Description = p.Description ?? string.Empty,
                        Price = p.Price
                    });
                }
            }
        }
    }
}
