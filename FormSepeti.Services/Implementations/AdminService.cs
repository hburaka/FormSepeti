// FormSepeti.Services/Implementations/AdminService.cs
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BCrypt.Net;
using Microsoft.Extensions.Logging;
using FormSepeti.Data.Entities;
using FormSepeti.Data.Repositories.Interfaces;
using FormSepeti.Services.Interfaces;

namespace FormSepeti.Services.Implementations
{
    public class AdminService : IAdminService
    {
        private readonly IAdminUserRepository _adminRepository;
        private readonly IAuditLogService _auditLogService;
        private readonly ILogger<AdminService> _logger;

        public AdminService(
            IAdminUserRepository adminRepository,
            IAuditLogService auditLogService,
            ILogger<AdminService> logger)
        {
            _adminRepository = adminRepository;
            _auditLogService = auditLogService;
            _logger = logger;
        }

        public async Task<AdminUser?> AuthenticateAsync(string username, string password)
        {
            try
            {
                var admin = await _adminRepository.GetByUsernameAsync(username);

                if (admin == null || !admin.IsActive)
                {
                    _logger.LogWarning($"Admin login failed - user not found or inactive: {username}");
                    return null;
                }

                if (!BCrypt.Net.BCrypt.Verify(password, admin.PasswordHash))
                {
                    _logger.LogWarning($"Admin login failed - invalid password: {username}");
                    await _auditLogService.LogAsync(
                        adminId: admin.AdminId,
                        action: "Failed Login Attempt",
                        entityType: "AdminUser",
                        entityId: admin.AdminId,
                        details: "Invalid password",
                        ipAddress: "system"
                    );
                    return null;
                }

                admin.LastLoginDate = DateTime.UtcNow;
                await _adminRepository.UpdateAsync(admin);

                await _auditLogService.LogAsync(
                    adminId: admin.AdminId,
                    action: "Admin Login",
                    entityType: "AdminUser",
                    entityId: admin.AdminId,
                    details: $"Admin {admin.Username} logged in successfully",
                    ipAddress: "system"
                );

                _logger.LogInformation($"Admin authenticated successfully: {username}");
                return admin;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error authenticating admin: {username}");
                return null;
            }
        }

        public async Task<bool> ChangePasswordAsync(int adminId, string oldPassword, string newPassword)
        {
            try
            {
                var admin = await _adminRepository.GetByIdAsync(adminId);
                if (admin == null) return false;

                if (!BCrypt.Net.BCrypt.Verify(oldPassword, admin.PasswordHash))
                {
                    _logger.LogWarning($"Password change failed - invalid old password: AdminId={adminId}");
                    return false;
                }

                if (!ValidatePassword(newPassword, out string? errorMessage))
                {
                    _logger.LogWarning($"Password change failed - validation error: {errorMessage}");
                    return false;
                }

                admin.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
                var result = await _adminRepository.UpdateAsync(admin);

                if (result)
                {
                    await _auditLogService.LogAsync(
                        adminId: adminId,
                        action: "Password Changed",
                        entityType: "AdminUser",
                        entityId: adminId,
                        details: "Admin changed their password",
                        ipAddress: "system"
                    );
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error changing password for admin: {adminId}");
                return false;
            }
        }

        public async Task<AdminUser?> GetAdminByIdAsync(int adminId)
        {
            return await _adminRepository.GetByIdAsync(adminId);
        }

        public async Task<AdminUser?> GetAdminByUsernameAsync(string username)
        {
            return await _adminRepository.GetByUsernameAsync(username);
        }

        public async Task<List<AdminUser>> GetAllAdminsAsync()
        {
            return await _adminRepository.GetAllAsync();
        }

        public async Task<List<AdminUser>> GetAllActiveAdminsAsync()
        {
            return await _adminRepository.GetAllActiveAsync();
        }

        public async Task<AdminUser> CreateAdminAsync(string username, string password, string fullName, string email, string role)
        {
            try
            {
                if (!ValidatePassword(password, out string? errorMessage))
                {
                    throw new InvalidOperationException(errorMessage);
                }

                if (await UsernameExistsAsync(username))
                {
                    throw new InvalidOperationException("Bu kullanýcý adý zaten kullanýlýyor.");
                }

                if (await EmailExistsAsync(email))
                {
                    throw new InvalidOperationException("Bu e-posta adresi zaten kullanýlýyor.");
                }

                var admin = new AdminUser
                {
                    Username = username,
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
                    FullName = fullName,
                    Email = email,
                    Role = role,
                    IsActive = true,
                    CreatedDate = DateTime.UtcNow
                };

                var createdAdmin = await _adminRepository.CreateAsync(admin);

                await _auditLogService.LogAsync(
                    adminId: null,
                    action: "Admin Created",
                    entityType: "AdminUser",
                    entityId: createdAdmin.AdminId,
                    details: $"New admin created: {username} with role {role}",
                    ipAddress: "system"
                );

                _logger.LogInformation($"Admin created successfully: {username}");
                return createdAdmin;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error creating admin: {username}");
                throw;
            }
        }

        public async Task<bool> UpdateAdminAsync(AdminUser admin)
        {
            try
            {
                var result = await _adminRepository.UpdateAsync(admin);

                if (result)
                {
                    await _auditLogService.LogAsync(
                        adminId: admin.AdminId,
                        action: "Admin Updated",
                        entityType: "AdminUser",
                        entityId: admin.AdminId,
                        details: $"Admin profile updated: {admin.Username}",
                        ipAddress: "system"
                    );
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error updating admin: {admin.AdminId}");
                return false;
            }
        }

        public async Task<bool> DeleteAdminAsync(int adminId)
        {
            try
            {
                var admin = await _adminRepository.GetByIdAsync(adminId);
                if (admin == null) return false;

                var result = await _adminRepository.DeleteAsync(adminId);

                if (result)
                {
                    await _auditLogService.LogAsync(
                        adminId: null,
                        action: "Admin Deleted",
                        entityType: "AdminUser",
                        entityId: adminId,
                        details: $"Admin deleted: {admin.Username}",
                        ipAddress: "system"
                    );
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error deleting admin: {adminId}");
                return false;
            }
        }

        public async Task<bool> ToggleAdminStatusAsync(int adminId)
        {
            try
            {
                var admin = await _adminRepository.GetByIdAsync(adminId);
                if (admin == null) return false;

                admin.IsActive = !admin.IsActive;
                var result = await _adminRepository.UpdateAsync(admin);

                if (result)
                {
                    await _auditLogService.LogAsync(
                        adminId: null,
                        action: admin.IsActive ? "Admin Activated" : "Admin Deactivated",
                        entityType: "AdminUser",
                        entityId: adminId,
                        details: $"Admin status changed: {admin.Username}",
                        ipAddress: "system"
                    );
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error toggling admin status: {adminId}");
                return false;
            }
        }

        public async Task<bool> UsernameExistsAsync(string username)
        {
            return await _adminRepository.UsernameExistsAsync(username);
        }

        public async Task<bool> EmailExistsAsync(string email)
        {
            return await _adminRepository.EmailExistsAsync(email);
        }

        public async Task<int> GetTotalAdminCountAsync()
        {
            return await _adminRepository.GetTotalAdminCountAsync();
        }

        public async Task<bool> ResetAdminPasswordAsync(int adminId, string newPassword)
        {
            try
            {
                if (!ValidatePassword(newPassword, out string? errorMessage))
                {
                    _logger.LogWarning($"Password reset failed - validation error: {errorMessage}");
                    return false;
                }

                var admin = await _adminRepository.GetByIdAsync(adminId);
                if (admin == null) return false;

                admin.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
                var result = await _adminRepository.UpdateAsync(admin);

                if (result)
                {
                    await _auditLogService.LogAsync(
                        adminId: null,
                        action: "Password Reset",
                        entityType: "AdminUser",
                        entityId: adminId,
                        details: "Admin password was reset",
                        ipAddress: "system"
                    );
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error resetting password for admin: {adminId}");
                return false;
            }
        }

        private bool ValidatePassword(string password, out string? errorMessage)
        {
            errorMessage = null;

            if (string.IsNullOrWhiteSpace(password))
            {
                errorMessage = "Þifre boþ olamaz.";
                return false;
            }

            if (password.Length < 8)
            {
                errorMessage = "Þifre en az 8 karakter olmalýdýr.";
                return false;
            }

            if (password.Length > 100)
            {
                errorMessage = "Þifre çok uzun.";
                return false;
            }

            return true;
        }
    }
}