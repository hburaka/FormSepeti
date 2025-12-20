using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using FormSepeti.Services.Interfaces;

namespace FormSepeti.Web.Middleware
{
    public class GoogleTokenRefreshMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<GoogleTokenRefreshMiddleware> _logger;

        public GoogleTokenRefreshMiddleware(
            RequestDelegate next,
            ILogger<GoogleTokenRefreshMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(
            HttpContext context,
            IUserService userService,
            IGoogleSheetsService googleSheetsService)
        {
            // ✅ Sadece authenticated user'lar için çalış
            if (context.User.Identity?.IsAuthenticated == true)
            {
                var userIdClaim = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

                if (int.TryParse(userIdClaim, out var userId))
                {
                    try
                    {
                        var user = await userService.GetUserByIdAsync(userId);

                        if (user != null &&
                            !string.IsNullOrEmpty(user.GoogleRefreshToken) &&
                            user.GoogleTokenExpiry.HasValue &&
                            user.GoogleTokenExpiry.Value <= DateTime.UtcNow)
                        {
                            _logger.LogInformation($"🔄 Auto-refreshing expired token for UserId={userId}");

                            var refreshed = await googleSheetsService.RefreshAccessToken(userId);

                            if (refreshed)
                            {
                                _logger.LogInformation($"✅ Token auto-refreshed for UserId={userId}");
                            }
                            else
                            {
                                _logger.LogWarning($"⚠️ Token refresh failed for UserId={userId}");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"Error in GoogleTokenRefreshMiddleware for UserId={userId}");
                    }
                }
            }

            await _next(context);
        }
    }

    // Extension method for middleware registration
    public static class GoogleTokenRefreshMiddlewareExtensions
    {
        public static IApplicationBuilder UseGoogleTokenRefresh(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<GoogleTokenRefreshMiddleware>();
        }
    }
}