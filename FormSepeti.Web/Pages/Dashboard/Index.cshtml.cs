using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Security.Claims;
using System.Threading.Tasks;
using FormSepeti.Services.Interfaces;
using FormSepeti.Data.Repositories.Interfaces;

namespace FormSepeti.Web.Pages.Dashboard
{
    public class IndexModel : PageModel
    {
        private readonly IUserService _userService;
        private readonly IPackageService _packageService;
        private readonly IUserGoogleSheetsRepository _sheetsRepository;
        private readonly IFormSubmissionRepository _submissionRepository;

        public IndexModel(
            IUserService userService,
            IPackageService packageService,
            IUserGoogleSheetsRepository sheetsRepository,
            IFormSubmissionRepository submissionRepository)
        {
            _userService = userService;
            _packageService = packageService;
            _sheetsRepository = sheetsRepository;
            _submissionRepository = submissionRepository;
        }

        public string UserName { get; private set; } = "";
        public bool IsGoogleConnected { get; private set; }
        public int TotalSubmissions { get; private set; }
        public string SpreadsheetUrl { get; set; }

        private int GetUserId()
        {
            var v = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return int.TryParse(v, out var id) ? id : 0;
        }

        public async Task OnGetAsync()
        {
            var userId = GetUserId();
            if (userId == 0) return;

            var user = await _userService.GetUserByIdAsync(userId);
            UserName = user?.Email ?? user?.PhoneNumber ?? "";
            IsGoogleConnected = !string.IsNullOrEmpty(user?.GoogleRefreshToken);
            TotalSubmissions = await _submissionRepository.GetCountByUserIdAsync(userId);
            SpreadsheetUrl = "https://docs.google.com/spreadsheets/..."; // Set this as appropriate
            // you can also load packages / available groups here if needed via _packageService
        }
    }
}
