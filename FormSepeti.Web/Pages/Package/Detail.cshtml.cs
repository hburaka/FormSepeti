using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using FormSepeti.Services.Interfaces;
using FormSepeti.Data.Repositories.Interfaces;
using FormSepeti.Data.Entities;
using Microsoft.Extensions.Logging;

namespace FormSepeti.Web.Pages.Package
{
    public class DetailModel : PageModel
    {
        private readonly IPackageService _packageService;
        private readonly IFormGroupRepository _formGroupRepository;
        private readonly IFormGroupMappingRepository _formGroupMappingRepository;
        private readonly ILogger<DetailModel> _logger;

        public DetailModel(
            IPackageService packageService,
            IFormGroupRepository formGroupRepository,
            IFormGroupMappingRepository formGroupMappingRepository,
            ILogger<DetailModel> logger)
        {
            _packageService = packageService;
            _formGroupRepository = formGroupRepository;
            _formGroupMappingRepository = formGroupMappingRepository;
            _logger = logger;
        }

        public FormSepeti.Data.Entities.Package Package { get; set; }
        public FormGroup FormGroup { get; set; }
        public List<FormGroupMapping> FormsInPackage { get; set; } = new();
        public bool? UserHasThisPackage { get; set; } // ? Nullable yapýldý (login deðilse null)
        public int TotalForms { get; set; }
        public int FreeForms { get; set; }
        public bool IsUserLoggedIn { get; set; } // ? Login durumu

        private int GetUserId()
        {
            var nameIdentifier = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var userIdClaim = User.FindFirst("UserId")?.Value;
            
            _logger.LogInformation($"GetUserId - NameIdentifier: {nameIdentifier}, UserIdClaim: {userIdClaim}");
            
            // Önce NameIdentifier'ý dene
            if (int.TryParse(nameIdentifier, out var id1))
            {
                return id1;
            }
            
            // Sonra UserId claim'ini dene
            if (int.TryParse(userIdClaim, out var id2))
            {
                return id2;
            }
            
            return 0;
        }

        public async Task<IActionResult> OnGetAsync(int id)
        {
            var userId = GetUserId();
            IsUserLoggedIn = userId > 0;
            
            _logger.LogInformation($"PackageDetail - UserId: {userId}, PackageId: {id}, IsLoggedIn: {IsUserLoggedIn}");

            // ? Login kontrolü kaldýrýldý - herkes görebilsin
            
            // Paket bilgilerini al
            Package = await _packageService.GetPackageByIdAsync(id);
            if (Package == null)
            {
                _logger.LogWarning($"PackageDetail - Package not found: {id}");
                return NotFound();
            }

            // Grup bilgilerini al
            FormGroup = await _formGroupRepository.GetByIdAsync(Package.GroupId);

            // Gruptaki formlarý al
            FormsInPackage = await _formGroupMappingRepository.GetByGroupIdAsync(Package.GroupId);

            // Ýstatistikler
            TotalForms = FormsInPackage.Count;
            FreeForms = FormsInPackage.Count(f => f.IsFreeInGroup && !f.RequiresPackage);

            // ? Kullanýcýnýn bu pakete sahip olup olmadýðýný kontrol et (sadece login ise)
            if (IsUserLoggedIn)
            {
                UserHasThisPackage = await _packageService.HasActivePackageForGroupAsync(userId, Package.GroupId);
                _logger.LogInformation($"PackageDetail - UserHasThisPackage: {UserHasThisPackage}, GroupId: {Package.GroupId}");
            }
            else
            {
                UserHasThisPackage = null; // Login deðilse null
            }

            return Page();
        }
    }
}
