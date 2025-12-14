using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading.Tasks;
using FormSepeti.Services.Interfaces;

namespace FormSepeti.Web.Controllers
{
    [Authorize]
    public class GoogleAuthController : Controller
    {
        private readonly IGoogleSheetsService _googleSheetsService;
        private readonly IUserService _userService;

        public GoogleAuthController(
            IGoogleSheetsService googleSheetsService,
            IUserService userService)
        {
            _googleSheetsService = googleSheetsService;
            _userService = userService;
        }

        [HttpGet]
        public async Task<IActionResult> Connect()
        {
            try
            {
                var userId = GetCurrentUserId();
                var authUrl = await _googleSheetsService.GetAuthorizationUrl(userId);

                if (string.IsNullOrEmpty(authUrl))
                {
                    TempData["Error"] = "Google bağlantısı oluşturulamadı.";
                    return RedirectToAction("Index", "Dashboard");
                }

                return Redirect(authUrl);
            }
            catch (Exception)
            {
                TempData["Error"] = "Bir hata oluştu.";
                return RedirectToAction("Index", "Dashboard");
            }
        }

        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> Callback(string code, string state, string error)
        {
            try
            {
                if (!string.IsNullOrEmpty(error))
                {
                    TempData["Error"] = "Google bağlantısı reddedildi.";
                    return RedirectToAction("Index", "Dashboard");
                }

                if (string.IsNullOrEmpty(code) || string.IsNullOrEmpty(state))
                {
                    TempData["Error"] = "Geçersiz callback verisi.";
                    return RedirectToAction("Index", "Dashboard");
                }

                var userId = int.Parse(state);
                var success = await _googleSheetsService.HandleOAuthCallback(userId, code);

                if (success)
                {
                    TempData["Success"] = "Google Sheets başarıyla bağlandı!";
                }
                else
                {
                    TempData["Error"] = "Google bağlantısı kurulamadı.";
                }

                return RedirectToAction("Index", "Dashboard");
            }
            catch (Exception)
            {
                TempData["Error"] = "Bir hata oluştu.";
                return RedirectToAction("Index", "Dashboard");
            }
        }

        [HttpPost]
        public async Task<IActionResult> Disconnect()
        {
            try
            {
                var userId = GetCurrentUserId();
                var user = await _userService.GetUserByIdAsync(userId);

                if (user != null)
                {
                    user.GoogleAccessToken = null;
                    user.GoogleRefreshToken = null;
                    user.GoogleTokenExpiry = null;
                    // UpdateAsync metodunu UserService'e ekleyeceksiniz
                    TempData["Success"] = "Google Sheets bağlantısı kesildi.";
                }

                return RedirectToAction("Index", "Dashboard");
            }
            catch (Exception)
            {
                TempData["Error"] = "Bir hata oluştu.";
                return RedirectToAction("Index", "Dashboard");
            }
        }

        private int GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst("UserId")?.Value;
            return int.Parse(userIdClaim ?? "0");
        }
    }
}