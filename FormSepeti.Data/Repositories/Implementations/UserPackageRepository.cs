using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using FormSepeti.Data.Entities;
using FormSepeti.Data.Repositories.Interfaces;

namespace FormSepeti.Data.Repositories.Implementations
{
    public class UserPackageRepository : IUserPackageRepository
    {
        private readonly ApplicationDbContext _context;

        public UserPackageRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<UserPackage> GetByIdAsync(int userPackageId)
        {
            return await _context.UserPackages
                .Include(up => up.Package)
                .Include(up => up.FormGroup)
                .FirstOrDefaultAsync(up => up.UserPackageId == userPackageId);
        }

        public async Task<List<UserPackage>> GetActiveByUserIdAsync(int userId)
        {
            return await _context.UserPackages
                .Where(up => up.UserId == userId
                    && up.IsActive
                    && (up.ExpiryDate == null || up.ExpiryDate > DateTime.UtcNow))
                .Include(up => up.Package)
                .Include(up => up.FormGroup)
                .OrderByDescending(up => up.PurchaseDate)
                .ToListAsync();
        }

        public async Task<List<FormGroup>> GetActiveGroupsByUserIdAsync(int userId)
        {
            return await _context.UserPackages
                .Where(up => up.UserId == userId
                    && up.IsActive
                    && (up.ExpiryDate == null || up.ExpiryDate > DateTime.UtcNow))
                .Select(up => up.FormGroup)
                .Distinct()
                .ToListAsync();
        }

        public async Task<bool> HasActivePackageAsync(int userId, int groupId)
        {
            return await _context.UserPackages
                .AnyAsync(up => up.UserId == userId
                    && up.GroupId == groupId
                    && up.IsActive
                    && (up.ExpiryDate == null || up.ExpiryDate > DateTime.UtcNow));
        }

        public async Task<UserPackage> CreateAsync(UserPackage userPackage)
        {
            _context.UserPackages.Add(userPackage);
            await _context.SaveChangesAsync();
            return userPackage;
        }

        public async Task<bool> UpdateAsync(UserPackage userPackage)
        {
            _context.UserPackages.Update(userPackage);
            return await _context.SaveChangesAsync() > 0;
        }

        public async Task<bool> DeleteAsync(int userPackageId)
        {
            var userPackage = await GetByIdAsync(userPackageId);
            if (userPackage == null) return false;

            _context.UserPackages.Remove(userPackage);
            return await _context.SaveChangesAsync() > 0;
        }
    }
}