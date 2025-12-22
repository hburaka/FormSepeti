// FormSepeti.Data/Repositories/Interfaces/IAdminUserRepository.cs
using System.Collections.Generic;
using System.Threading.Tasks;
using FormSepeti.Data.Entities;

namespace FormSepeti.Data.Repositories.Interfaces
{
    public interface IAdminUserRepository
    {
        Task<AdminUser?> GetByIdAsync(int adminId);
        Task<AdminUser?> GetByUsernameAsync(string username);
        Task<AdminUser?> GetByEmailAsync(string email);
        Task<List<AdminUser>> GetAllAsync();
        Task<List<AdminUser>> GetAllActiveAsync();
        Task<AdminUser> CreateAsync(AdminUser adminUser);
        Task<bool> UpdateAsync(AdminUser adminUser);
        Task<bool> DeleteAsync(int adminId);
        Task<bool> UsernameExistsAsync(string username);
        Task<bool> EmailExistsAsync(string email);
        Task<int> GetTotalAdminCountAsync();
    }
}