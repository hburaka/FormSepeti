using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using FormSepeti.Data.Entities;
using FormSepeti.Data.Repositories.Interfaces;

namespace FormSepeti.Data.Repositories.Implementations
{
    public class UserGoogleSheetsRepository : IUserGoogleSheetsRepository
    {
        private readonly ApplicationDbContext _context;

        public UserGoogleSheetsRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<UserGoogleSheet> GetByIdAsync(int sheetId)
        {
            return await _context.UserGoogleSheets.FindAsync(sheetId);
        }

        public async Task<UserGoogleSheet> GetByUserAndGroupAsync(int userId, int groupId)
        {
            return await _context.UserGoogleSheets
                .FirstOrDefaultAsync(s => s.UserId == userId && s.GroupId == groupId);
        }

        public async Task<UserGoogleSheet> CreateAsync(UserGoogleSheet sheet)
        {
            _context.UserGoogleSheets.Add(sheet);
            await _context.SaveChangesAsync();
            return sheet;
        }

        public async Task<bool> UpdateAsync(UserGoogleSheet sheet)
        {
            _context.UserGoogleSheets.Update(sheet);
            return await _context.SaveChangesAsync() > 0;
        }

        public async Task<bool> DeleteAsync(int sheetId)
        {
            var sheet = await GetByIdAsync(sheetId);
            if (sheet == null) return false;

            _context.UserGoogleSheets.Remove(sheet);
            return await _context.SaveChangesAsync() > 0;
        }
    }
}