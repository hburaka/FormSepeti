// FormSepeti.Data/Repositories/Implementations/AdminUserRepository.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using FormSepeti.Data.Entities;
using FormSepeti.Data.Repositories.Interfaces;

namespace FormSepeti.Data.Repositories.Implementations
{
    public class AdminUserRepository : IAdminUserRepository
    {
        private readonly ApplicationDbContext _context;

        public AdminUserRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<AdminUser?> GetByIdAsync(int adminId)
        {
            return await _context.AdminUsers.FindAsync(adminId);
        }

        public async Task<AdminUser?> GetByUsernameAsync(string username)
        {
            return await _context.AdminUsers
                .FirstOrDefaultAsync(a => a.Username == username);
        }

        public async Task<AdminUser?> GetByEmailAsync(string email)
        {
            return await _context.AdminUsers
                .FirstOrDefaultAsync(a => a.Email == email);
        }

        public async Task<List<AdminUser>> GetAllAsync()
        {
            return await _context.AdminUsers
                .OrderByDescending(a => a.CreatedDate)
                .ToListAsync();
        }

        public async Task<List<AdminUser>> GetAllActiveAsync()
        {
            return await _context.AdminUsers
                .Where(a => a.IsActive)
                .OrderBy(a => a.FullName)
                .ToListAsync();
        }

        public async Task<AdminUser> CreateAsync(AdminUser adminUser)
        {
            adminUser.CreatedDate = DateTime.UtcNow;
            _context.AdminUsers.Add(adminUser);
            await _context.SaveChangesAsync();
            return adminUser;
        }

        public async Task<bool> UpdateAsync(AdminUser adminUser)
        {
            _context.AdminUsers.Update(adminUser);
            return await _context.SaveChangesAsync() > 0;
        }

        public async Task<bool> DeleteAsync(int adminId)
        {
            var admin = await GetByIdAsync(adminId);
            if (admin == null) return false;

            _context.AdminUsers.Remove(admin);
            return await _context.SaveChangesAsync() > 0;
        }

        public async Task<bool> UsernameExistsAsync(string username)
        {
            return await _context.AdminUsers
                .AnyAsync(a => a.Username == username);
        }

        public async Task<bool> EmailExistsAsync(string email)
        {
            return await _context.AdminUsers
                .AnyAsync(a => a.Email == email);
        }

        public async Task<int> GetTotalAdminCountAsync()
        {
            return await _context.AdminUsers.CountAsync();
        }
    }
}