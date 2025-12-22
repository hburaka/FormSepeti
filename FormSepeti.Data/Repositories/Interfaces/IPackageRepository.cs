using System.Collections.Generic;
using System.Threading.Tasks;
using FormSepeti.Data.Entities;

namespace FormSepeti.Data.Repositories.Interfaces
{
    public interface IPackageRepository
    {
        Task<Package> GetByIdAsync(int packageId);
        Task<List<Package>> GetByGroupIdAsync(int groupId);
        Task<List<Package>> GetAllActiveAsync();
        Task<List<Package>> GetAllAsync(); // ✅ Ekle
        Task<Package> CreateAsync(Package package);
        Task<bool> UpdateAsync(Package package);
        Task<bool> DeleteAsync(int packageId);
    }
}