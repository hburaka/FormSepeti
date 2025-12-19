using System.Collections.Generic;
using System.Threading.Tasks;
using FormSepeti.Data.Entities;

namespace FormSepeti.Data.Repositories.Interfaces
{
    public interface IUserGoogleSheetsRepository
    {
        Task<UserGoogleSheet> GetByIdAsync(int sheetId);
        Task<UserGoogleSheet> GetByUserAndGroupAsync(int userId, int groupId);
        Task<UserGoogleSheet> CreateAsync(UserGoogleSheet sheet);
        Task<bool> UpdateAsync(UserGoogleSheet sheet);
        Task<bool> DeleteAsync(int sheetId);
        Task<List<UserGoogleSheet>> GetByUserIdAsync(int userId);
    }
}