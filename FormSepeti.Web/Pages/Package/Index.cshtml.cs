using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Collections.Generic;
using System.Threading.Tasks;
using FormSepeti.Services.Interfaces;
using FormSepeti.Data.Repositories.Interfaces;

namespace FormSepeti.Web.Pages.Package
{
    public class IndexModel : PageModel
    {
        private readonly IFormGroupRepository _groupRepo;
        private readonly IPackageService _packageService;

        public IndexModel(IFormGroupRepository groupRepo, IPackageService packageService)
        {
            _groupRepo = groupRepo;
            _packageService = packageService;
        }

        // View model used by the Razor page
        public record PackageView(int PackageId, string Name, string Description, decimal Price, int GroupId, string GroupName);

        public List<PackageView> Packages { get; private set; } = new();

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
                    Packages.Add(new PackageView(
                        p.PackageId,
                        p.PackageName ?? string.Empty,
                        p.Description ?? string.Empty,
                        p.Price,
                        g.GroupId,
                        g.GroupName ?? string.Empty
                    ));
                }
            }
        }
    }
}
