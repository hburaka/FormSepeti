using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
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
            public string PackageName { get; set; }
            public decimal Price { get; set; }
            public string Description { get; set; }
            public bool IsOwned { get; set; }
        }

        public List<PackageView> Packages { get; set; } = new List<PackageView>();

        // ✅ Sıralama parametreleri
        [BindProperty(SupportsGet = true)]
        public string SortBy { get; set; } = "name"; // name, price, owned

        [BindProperty(SupportsGet = true)]
        public string SortOrder { get; set; } = "asc"; // asc, desc

        [BindProperty(SupportsGet = true)]
        public string Filter { get; set; } = "all"; // all, owned, available, free

        public async Task OnGetAsync()
        {
            var userId = GetCurrentUserId();
            
            var userPackages = new List<int>();
            if (userId > 0)
            {
                var activePackages = await _packageService.GetUserActivePackagesAsync(userId);
                userPackages = activePackages.Select(p => p.PackageId).ToList();
            }

            var groups = await _groupRepo.GetAllActiveAsync();
            if (groups == null) return;

            foreach (var g in groups)
            {
                var pkgs = await _packageService.GetPackagesByGroupIdAsync(g.GroupId);
                if (pkgs == null) continue;

                foreach (var p in pkgs)
                {
                    Packages.Add(new PackageView
                    {
                        PackageId = p.PackageId,
                        PackageName = p.PackageName ?? string.Empty,
                        Description = p.Description ?? string.Empty,
                        Price = p.Price,
                        IsOwned = userPackages.Contains(p.PackageId)
                    });
                }
            }

            // ✅ Filtreleme uygula
            Packages = ApplyFilter(Packages, Filter);

            // ✅ Sıralama uygula
            Packages = ApplySort(Packages, SortBy, SortOrder);
        }

        // ✅ Filtreleme metodu
        private List<PackageView> ApplyFilter(List<PackageView> packages, string filter)
        {
            return filter switch
            {
                "owned" => packages.Where(p => p.IsOwned).ToList(),
                "available" => packages.Where(p => !p.IsOwned).ToList(),
                "free" => packages.Where(p => p.Price == 0).ToList(),
                _ => packages // "all"
            };
        }

        // ✅ Sıralama metodu
        private List<PackageView> ApplySort(List<PackageView> packages, string sortBy, string order)
        {
            IEnumerable<PackageView> sorted = sortBy switch
            {
                "price" => order == "asc" 
                    ? packages.OrderBy(p => p.Price)
                    : packages.OrderByDescending(p => p.Price),
                "owned" => order == "asc"
                    ? packages.OrderBy(p => p.IsOwned)
                    : packages.OrderByDescending(p => p.IsOwned),
                _ => order == "asc" // "name"
                    ? packages.OrderBy(p => p.PackageName)
                    : packages.OrderByDescending(p => p.PackageName)
            };

            return sorted.ToList();
        }

        private int GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value 
                           ?? User.FindFirst("UserId")?.Value;
            return int.TryParse(userIdClaim, out var id) ? id : 0;
        }
    }
}
