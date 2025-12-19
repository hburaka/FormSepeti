using System.Threading.Tasks;
using FormSepeti.Data.Entities;

namespace FormSepeti.Data.Repositories.Interfaces
{
    public interface IUserRepository
    {
        Task<User?> GetByIdAsync(int userId); // ✅ Nullable
        Task<User?> GetByEmailAsync(string email); // ✅ Nullable
        Task<User?> GetByActivationTokenAsync(string token); // ✅ Nullable
        Task<User> CreateAsync(User user);
        Task<bool> UpdateAsync(User user);
        Task<bool> DeleteAsync(int userId);
        Task<User?> GetByEmailOrPhoneAsync(string emailOrPhone); // ✅ Nullable
        Task<User?> GetByGoogleIdAsync(string googleId); // ✅ Nullable
    }
}