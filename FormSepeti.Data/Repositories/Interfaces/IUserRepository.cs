using System.Threading.Tasks;
using FormSepeti.Data.Entities;

namespace FormSepeti.Data.Repositories.Interfaces
{
    public interface IUserRepository
    {
        Task<User?> GetByIdAsync(int userId);
        Task<User?> GetByEmailAsync(string email);
        Task<User?> GetByActivationTokenAsync(string token);
        Task<User> CreateAsync(User user);
        Task<bool> UpdateAsync(User user);
        Task<bool> DeleteAsync(int userId);
        Task<User?> GetByEmailOrPhoneAsync(string emailOrPhone);
        Task<User?> GetByGoogleIdAsync(string googleId);
        
        // ✅ Fatura bilgileri için
        Task<User?> GetByTCKimlikNoAsync(string tcKimlikNo);
        Task<User?> GetByTaxNumberAsync(string taxNumber);
    }
}