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
    public class CreateModel : PageModel
    {
        private readonly IAdminService _adminService;
        private readonly IAuditLogService _auditLogService;
        private readonly ILogger<CreateModel> _logger;

        public CreateModel(
            IAdminService adminService,
            IAuditLogService auditLogService,
            ILogger<CreateModel> logger)
        {
            _adminService = adminService;
            _auditLogService = auditLogService;
            _logger = logger;
        }

        [BindProperty]
        public AdminCreateViewModel Input { get; set; } = new();

        public void OnGet()
        {
            _logger.LogInformation("SuperAdmin creating new admin");
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                return Page();
            }

            _logger.LogInformation("SuperAdmin saving new admin - Username: {Username}", Input.Username);

            // Username benzersizlik kontrolü
            if (await _adminService.UsernameExistsAsync(Input.Username))
            {
                ModelState.AddModelError("Input.Username", "Bu kullanýcý adý zaten kullanýlýyor.");
                return Page();
            }

            // Email benzersizlik kontrolü
            if (await _adminService.EmailExistsAsync(Input.Email))
            {
                ModelState.AddModelError("Input.Email", "Bu email adresi zaten kullanýlýyor.");
                return Page();
            }

            // Þifre onayý kontrolü
            if (Input.Password != Input.ConfirmPassword)
            {
                ModelState.AddModelError("Input.ConfirmPassword", "Þifreler eþleþmiyor.");
                return Page();
            }

            // Yeni admin oluþtur
            var admin = await _adminService.CreateAdminAsync(
                Input.Username,
                Input.Password,
                Input.FullName,
                Input.Email,
                Input.Role
            );

            // Audit log
            var currentAdminId = int.Parse(User.FindFirst("AdminId")?.Value ?? "0");
            await _auditLogService.LogAsync(
                action: "AdminCreated",
                entityType: "AdminUser",
                entityId: admin.AdminId,
                details: $"Yeni admin oluþturuldu: {Input.Username} ({Input.Role})",
                adminId: currentAdminId,
                ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString()
            );

            TempData["SuccessMessage"] = $"Admin baþarýyla oluþturuldu: {Input.Username}";
            return RedirectToPage("/Admins/Index");
        }

        public class AdminCreateViewModel
        {
            [Required(ErrorMessage = "Kullanýcý adý zorunludur.")]
            [StringLength(50, MinimumLength = 3, ErrorMessage = "Kullanýcý adý 3-50 karakter arasýnda olmalýdýr.")]
            [RegularExpression(@"^[a-zA-Z0-9_]+$", ErrorMessage = "Kullanýcý adý sadece harf, rakam ve alt çizgi içerebilir.")]
            [Display(Name = "Kullanýcý Adý")]
            public string Username { get; set; } = string.Empty;

            [Required(ErrorMessage = "Þifre zorunludur.")]
            [StringLength(100, MinimumLength = 6, ErrorMessage = "Þifre en az 6 karakter olmalýdýr.")]
            [DataType(DataType.Password)]
            [Display(Name = "Þifre")]
            public string Password { get; set; } = string.Empty;

            [Required(ErrorMessage = "Þifre onayý zorunludur.")]
            [DataType(DataType.Password)]
            [Display(Name = "Þifre Onayý")]
            [Compare("Password", ErrorMessage = "Þifreler eþleþmiyor.")]
            public string ConfirmPassword { get; set; } = string.Empty;

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
        }
    }
}