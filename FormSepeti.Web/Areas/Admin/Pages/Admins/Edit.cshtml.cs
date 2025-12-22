using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using FormSepeti.Services.Interfaces;
using Microsoft.Extensions.Logging;

namespace FormSepeti.Web.Areas.Admin.Pages.Admins
{
    [Authorize(Policy = "SuperAdminOnly")]
    public class EditModel : PageModel
    {
        private readonly IAdminService _adminService;
        private readonly IAuditLogService _auditLogService;
        private readonly ILogger<EditModel> _logger;

        public EditModel(
            IAdminService adminService,
            IAuditLogService auditLogService,
            ILogger<EditModel> logger)
        {
            _adminService = adminService;
            _auditLogService = auditLogService;
            _logger = logger;
        }

        [BindProperty]
        public AdminEditViewModel Input { get; set; } = new();

        public async Task<IActionResult> OnGetAsync(int id)
        {
            _logger.LogInformation("SuperAdmin editing admin - AdminId: {AdminId}", id);

            var admin = await _adminService.GetAdminByIdAsync(id);
            if (admin == null)
            {
                TempData["ErrorMessage"] = "Admin bulunamadý.";
                return RedirectToPage("/Admins/Index");
            }

            Input = new AdminEditViewModel
            {
                AdminId = admin.AdminId,
                Username = admin.Username,
                FullName = admin.FullName,
                Email = admin.Email,
                Role = admin.Role,
                IsActive = admin.IsActive
            };

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                return Page();
            }

            _logger.LogInformation("SuperAdmin saving admin changes - AdminId: {AdminId}", Input.AdminId);

            // Kendini düzenlerken rol deðiþikliðini engelle
            var currentAdminId = int.Parse(User.FindFirst("AdminId")?.Value ?? "0");
            if (currentAdminId == Input.AdminId)
            {
                var currentAdmin = await _adminService.GetAdminByIdAsync(currentAdminId);
                if (currentAdmin?.Role != Input.Role)
                {
                    ModelState.AddModelError("Input.Role", "Kendi rolünüzü deðiþtiremezsiniz.");
                    return Page();
                }
            }

            var admin = await _adminService.GetAdminByIdAsync(Input.AdminId);
            if (admin == null)
            {
                TempData["ErrorMessage"] = "Admin bulunamadý.";
                return RedirectToPage("/Admins/Index");
            }

            // Email deðiþikliði kontrolü
            if (admin.Email != Input.Email)
            {
                if (await _adminService.EmailExistsAsync(Input.Email))
                {
                    var existingAdmin = await _adminService.GetAllAdminsAsync();
                    if (existingAdmin.Any(a => a.Email == Input.Email && a.AdminId != Input.AdminId))
                    {
                        ModelState.AddModelError("Input.Email", "Bu email adresi baþka bir admin tarafýndan kullanýlýyor.");
                        return Page();
                    }
                }
            }

            // Güncelleme
            admin.FullName = Input.FullName;
            admin.Email = Input.Email;
            admin.Role = Input.Role;
            admin.IsActive = Input.IsActive;

            await _adminService.UpdateAdminAsync(admin);

            // Þifre deðiþikliði varsa
            if (!string.IsNullOrWhiteSpace(Input.NewPassword))
            {
                await _adminService.ResetAdminPasswordAsync(Input.AdminId, Input.NewPassword);
            }

            // Audit log
            await _auditLogService.LogAsync(
                action: "AdminUpdated",
                entityType: "AdminUser",
                entityId: admin.AdminId,
                details: $"Admin güncellendi: {Input.Username} ({Input.Role})",
                adminId: currentAdminId,
                ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString()
            );

            TempData["SuccessMessage"] = "Admin baþarýyla güncellendi.";
            return RedirectToPage("/Admins/Index");
        }

        public class AdminEditViewModel
        {
            public int AdminId { get; set; }

            [Display(Name = "Kullanýcý Adý")]
            public string Username { get; set; } = string.Empty;

            [Required(ErrorMessage = "Ad Soyad zorunludur.")]
            [StringLength(100, ErrorMessage = "Ad Soyad en fazla 100 karakter olabilir.")]
            [Display(Name = "Ad Soyad")]
            public string FullName { get; set; } = string.Empty;

            [Required(ErrorMessage = "Email zorunludur.")]
            [EmailAddress(ErrorMessage = "Geçerli bir email adresi giriniz.")]
            [Display(Name = "Email")]
            public string Email { get; set; } = string.Empty;

            [Required(ErrorMessage = "Rol seçimi zorunludur.")]
            [Display(Name = "Rol")]
            public string Role { get; set; } = "Admin";

            [Display(Name = "Aktif")]
            public bool IsActive { get; set; }

            [StringLength(100, MinimumLength = 6, ErrorMessage = "Þifre en az 6 karakter olmalýdýr.")]
            [DataType(DataType.Password)]
            [Display(Name = "Yeni Þifre (Deðiþtirmek istemiyorsanýz boþ býrakýn)")]
            public string? NewPassword { get; set; }

            [DataType(DataType.Password)]
            [Display(Name = "Yeni Þifre Onayý")]
            [Compare("NewPassword", ErrorMessage = "Þifreler eþleþmiyor.")]
            public string? ConfirmNewPassword { get; set; }
        }
    }
}