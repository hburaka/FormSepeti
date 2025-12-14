using System.Collections.Generic;
using System.Threading.Tasks;
using FormSepeti.Data.Entities;

namespace FormSepeti.Data.Repositories.Interfaces
{
    public interface IUserPackageRepository
    {
        Task<UserPackage> GetByIdAsync(int userPackageId);
        Task<List<UserPackage>> GetActiveByUserIdAsync(int userId);
        Task<List<FormGroup>> GetActiveGroupsByUserIdAsync(int userId);
        Task<bool> HasActivePackageAsync(int userId, int groupId);
        Task<UserPackage> CreateAsync(UserPackage userPackage);
        Task<bool> UpdateAsync(UserPackage userPackage);
        Task<bool> DeleteAsync(int userPackageId);
    }
}