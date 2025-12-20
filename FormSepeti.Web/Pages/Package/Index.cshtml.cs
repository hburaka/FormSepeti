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
        private readonly IFormGroupMappingRepository _formGroupMappingRepository;

        public IndexModel(
            IFormGroupRepository groupRepo, 
            IPackageService packageService,
            IFormGroupMappingRepository formGroupMappingRepository)
        {
            _groupRepo = groupRepo;
            _packageService = packageService;
            _formGroupMappingRepository = formGroupMappingRepository;
        }

        public class PackageView
        {
            public int PackageId { get; set; }
            public string PackageName { get; set; } = string.Empty;
            public decimal Price { get; set; }
            public string Description { get; set; } = string.Empty;
            public bool IsOwned { get; set; }
            public int GroupId { get; set; }
            public List<string> FormNames { get; set; } = new List<string>();
            public int FormCount { get; set; }
        }

        public List<PackageView> Packages { get; set; } = new List<PackageView>();

        // Filtreleme ve Sıralama
        [BindProperty(SupportsGet = true)]
        public string SortBy { get; set; } = "name";

        [BindProperty(SupportsGet = true)]
        public string SortOrder { get; set; } = "asc";

        [BindProperty(SupportsGet = true)]
        public string Filter { get; set; } = "all";

        [BindProperty(SupportsGet = true)]
        public string SearchQuery { get; set; } = string.Empty;

        // ✅ YENİ: Pagination parametreleri
        [BindProperty(SupportsGet = true)]
        public int PageNumber { get; set; } = 1;

        [BindProperty(SupportsGet = true)]
        public int PageSize { get; set; } = 9; // Varsayılan: 9 paket (3x3 grid)

        // ✅ YENİ: Pagination bilgileri
        public int TotalItems { get; set; }
        public int TotalPages { get; set; }
        public bool HasPreviousPage => PageNumber > 1;
        public bool HasNextPage => PageNumber < TotalPages;
        public int StartItem => TotalItems == 0 ? 0 : (PageNumber - 1) * PageSize + 1;
        public int EndItem => Math.Min(PageNumber * PageSize, TotalItems);

        // ✅ YENİ: Sayfa numaraları listesi (pagination UI için)
        public List<int> PageNumbers { get; set; } = new List<int>();

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

            var allPackages = new List<PackageView>();

            foreach (var g in groups)
            {
                var pkgs = await _packageService.GetPackagesByGroupIdAsync(g.GroupId);
                if (pkgs == null) continue;

                var formMappings = await _formGroupMappingRepository.GetByGroupIdAsync(g.GroupId);
                var formNames = formMappings
                    .Where(m => m.Form != null)
                    .Select(m => m.Form.FormName)
                    .ToList();

                foreach (var p in pkgs)
                {
                    allPackages.Add(new PackageView
                    {
                        PackageId = p.PackageId,
                        PackageName = p.PackageName ?? string.Empty,
                        Description = p.Description ?? string.Empty,
                        Price = p.Price,
                        IsOwned = userPackages.Contains(p.PackageId),
                        GroupId = g.GroupId,
                        FormNames = formNames,
                        FormCount = formNames.Count
                    });
                }
            }

            // Arama uygula
            if (!string.IsNullOrWhiteSpace(SearchQuery))
            {
                allPackages = ApplySearch(allPackages, SearchQuery);
            }

            // Filtreleme uygula
            allPackages = ApplyFilter(allPackages, Filter);

            // Sıralama uygula
            allPackages = ApplySort(allPackages, SortBy, SortOrder);

            // ✅ YENİ: Pagination hesaplamaları
            TotalItems = allPackages.Count;
            TotalPages = (int)Math.Ceiling(TotalItems / (double)PageSize);

            // Sayfa numarası sınırlarını kontrol et
            if (PageNumber < 1) PageNumber = 1;
            if (PageNumber > TotalPages && TotalPages > 0) PageNumber = TotalPages;

            // ✅ YENİ: Sayfa numaraları listesi oluştur (max 7 sayfa göster)
            PageNumbers = GeneratePageNumbers(PageNumber, TotalPages);

            // ✅ YENİ: Sadece mevcut sayfadaki öğeleri al
            Packages = allPackages
                .Skip((PageNumber - 1) * PageSize)
                .Take(PageSize)
                .ToList();
        }

        // ✅ YENİ: Sayfa numaraları oluştur (ellipsis ile)
        private List<int> GeneratePageNumbers(int currentPage, int totalPages)
        {
            var pages = new List<int>();
            
            if (totalPages <= 7)
            {
                // 7 veya daha az sayfa varsa hepsini göster
                pages = Enumerable.Range(1, totalPages).ToList();
            }
            else
            {
                // İlk sayfa
                pages.Add(1);

                if (currentPage > 3)
                {
                    pages.Add(-1); // Ellipsis göstergesi
                }

                // Mevcut sayfanın etrafındaki sayfalar
                int start = Math.Max(2, currentPage - 1);
                int end = Math.Min(totalPages - 1, currentPage + 1);

                for (int i = start; i <= end; i++)
                {
                    if (!pages.Contains(i))
                    {
                        pages.Add(i);
                    }
                }

                if (currentPage < totalPages - 2)
                {
                    pages.Add(-1); // Ellipsis göstergesi
                }

                // Son sayfa
                if (!pages.Contains(totalPages))
                {
                    pages.Add(totalPages);
                }
            }

            return pages;
        }

        // ✅ Case-insensitive ve Türkçe karakter duyarsız arama
        private List<PackageView> ApplySearch(List<PackageView> packages, string query)
        {
            // Türkçe karakterleri normalize et
            query = query.Trim()
                .ToLowerInvariant() // Küçük harfe çevir
                .Replace('ı', 'i')
                .Replace('İ', 'i')
                .Replace('ş', 's')
                .Replace('Ş', 's')
                .Replace('ğ', 'g')
                .Replace('Ğ', 'g')
                .Replace('ü', 'u')
                .Replace('Ü', 'u')
                .Replace('ö', 'o')
                .Replace('Ö', 'o')
                .Replace('ç', 'c')
                .Replace('Ç', 'c');
            
            return packages.Where(p =>
            {
                // Paket adını normalize et
                var packageName = NormalizeText(p.PackageName);
                var description = NormalizeText(p.Description);
                
                // Form adlarını normalize et
                var formNamesNormalized = p.FormNames
                    .Select(f => NormalizeText(f))
                    .ToList();
                
                return packageName.Contains(query) ||
                       description.Contains(query) ||
                       formNamesNormalized.Any(f => f.Contains(query));
            }).ToList();
        }

        // ✅ Yardımcı metod: Text normalize
        private string NormalizeText(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return string.Empty;
            
            return text.ToLowerInvariant()
                .Replace('ı', 'i')
                .Replace('İ', 'i')
                .Replace('ş', 's')
                .Replace('Ş', 's')
                .Replace('ğ', 'g')
                .Replace('Ğ', 'g')
                .Replace('ü', 'u')
                .Replace('Ü', 'u')
                .Replace('ö', 'o')
                .Replace('Ö', 'o')
                .Replace('ç', 'c')
                .Replace('Ç', 'c');
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
