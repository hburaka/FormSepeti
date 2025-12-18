using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using FormSepeti.Services.Interfaces;
using FormSepeti.Data.Repositories.Interfaces;
using FormSepeti.Data.Entities;

namespace FormSepeti.Web.Pages.Package
{
    public class DetailModel : PageModel
    {
        private readonly IPackageService _packageService;
        private readonly IFormGroupRepository _formGroupRepository;
        private readonly IFormGroupMappingRepository _formGroupMappingRepository;

        public DetailModel(
            IPackageService packageService,
            IFormGroupRepository formGroupRepository,
            IFormGroupMappingRepository formGroupMappingRepository)
        {
            _packageService = packageService;
            _formGroupRepository = formGroupRepository;
            _formGroupMappingRepository = formGroupMappingRepository;
        }

        public Data.Entities.Package Package { get; set; }
        public FormGroup FormGroup { get; set; }
        public List<FormGroupMapping> FormsInPackage { get; set; } = new();
        public bool UserHasThisPackage { get; set; }
        public int TotalForms { get; set; }
        public int FreeForms { get; set; }

        private int GetUserId()
        {
            var v = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return int.TryParse(v, out var id) ? id : 0;
        }

        public async Task<IActionResult> OnGetAsync(int id)
        {
            var userId = GetUserId();
            if (userId == 0)
            {
                return RedirectToPage("/Account/Login");
            }

            // Paket bilgilerini al
            Package = await _packageService.GetPackageByIdAsync(id);
            if (Package == null)
            {
                return NotFound();
            }

            // Grup bilgilerini al
            FormGroup = await _formGroupRepository.GetByIdAsync(Package.GroupId);

            // Gruptaki formlarý al
            FormsInPackage = await _formGroupMappingRepository.GetByGroupIdAsync(Package.GroupId);

            // Ýstatistikler
            TotalForms = FormsInPackage.Count;
            FreeForms = FormsInPackage.Count(f => f.IsFreeInGroup && !f.RequiresPackage);

            // Kullanýcýnýn bu pakete sahip olup olmadýðýný kontrol et
            UserHasThisPackage = await _packageService.HasActivePackageForGroupAsync(userId, Package.GroupId);

            return Page();
        }
    }
}
