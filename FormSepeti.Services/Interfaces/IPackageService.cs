using System.Collections.Generic;
using System.Threading.Tasks;
using FormSepeti.Data.Entities;
using FormSepeti.Services.Models;

namespace FormSepeti.Services.Interfaces
{
    public interface IPackageService
    {
        Task<List<FormGroup>> GetAvailableGroupsForUserAsync(int userId);
        Task<List<Package>> GetPackagesByGroupIdAsync(int groupId);
        Task<Package> GetPackageByIdAsync(int packageId);
        Task<List<UserPackage>> GetUserActivePackagesAsync(int userId);
        Task<bool> HasActivePackageForGroupAsync(int userId, int groupId);
        Task<UserPackage> PurchasePackageAsync(int userId, int packageId, string transactionId, decimal amount);
        Task<bool> ActivatePackageAsync(int userPackageId);
        Task<bool> DeactivatePackageAsync(int userPackageId);
        Task<PackageAccessInfo> GetUserAccessToFormAsync(int userId, int formId, int groupId);
    }
}