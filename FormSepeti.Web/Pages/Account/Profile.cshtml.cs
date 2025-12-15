using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Security.Claims;
using System.Threading.Tasks;
using FormSepeti.Data.Repositories.Interfaces;

namespace FormSepeti.Web.Pages.Account
{
    public class ProfileModel : PageModel
    {
        private readonly IUserRepository _userRepository;
        public ProfileModel(IUserRepository userRepository) => _userRepository = userRepository;

        public string? Email { get; private set; }
        public string? Phone { get; private set; }
        public string CreatedDate { get; private set; } = "-";

        private int GetUserId()
        {
            var v = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return int.TryParse(v, out var id) ? id : 0;
        }

        public async Task OnGetAsync()
        {
            var id = GetUserId();
            if (id == 0) return;
            var u = await _userRepository.GetByIdAsync(id);
            if (u == null) return;
            Email = u.Email;
            Phone = u.PhoneNumber;
            CreatedDate = u.CreatedDate.ToString("yyyy-MM-dd");
        }
    }
}
