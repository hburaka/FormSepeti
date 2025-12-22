// FormSepeti.Services/Interfaces/IAdminService.cs
using System.Collections.Generic;
using System.Threading.Tasks;
using FormSepeti.Data.Entities;

namespace FormSepeti.Services.Interfaces
{
    public interface IAdminService
    {
        // Authentication
        Task<AdminUser?> AuthenticateAsync(string username, string password);
        Task<bool> ChangePasswordAsync(int adminId, string oldPassword, string newPassword);
        
        // CRUD Operations
        Task<AdminUser?> GetAdminByIdAsync(int adminId);
        Task<AdminUser?> GetAdminByUsernameAsync(string username);
        Task<List<AdminUser>> GetAllAdminsAsync();
        Task<List<AdminUser>> GetAllActiveAdminsAsync();
        Task<AdminUser> CreateAdminAsync(string username, string password, string fullName, string email, string role);
        Task<bool> UpdateAdminAsync(AdminUser admin);
        Task<bool> DeleteAdminAsync(int adminId);
        Task<bool> ToggleAdminStatusAsync(int adminId);
        
        // Validation
        Task<bool> UsernameExistsAsync(string username);
        Task<bool> EmailExistsAsync(string email);
        
        // Statistics
        Task<int> GetTotalAdminCountAsync();
        
        // Password Reset
        Task<bool> ResetAdminPasswordAsync(int adminId, string newPassword);
    }
}