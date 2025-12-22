using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using FormSepeti.Data.Entities;
using FormSepeti.Data.Repositories.Interfaces;

namespace FormSepeti.Data.Repositories.Implementations
{
    public class PackageRepository : IPackageRepository
    {
        private readonly ApplicationDbContext _context;

        public PackageRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<Package> GetByIdAsync(int packageId)
        {
            return await _context.Packages
                .Include(p => p.FormGroup)
                .FirstOrDefaultAsync(p => p.PackageId == packageId);
        }

        public async Task<List<Package>> GetByGroupIdAsync(int groupId)
        {
            return await _context.Packages
                .Where(p => p.GroupId == groupId && p.IsActive)
                .OrderBy(p => p.Price)
                .ToListAsync();
        }

        public async Task<List<Package>> GetAllActiveAsync()
        {
            return await _context.Packages
                .Include(p => p.FormGroup)
                .Where(p => p.IsActive)
                .OrderBy(p => p.Price)
                .ToListAsync();
        }

        public async Task<List<Package>> GetAllAsync()
        {
            return await _context.Packages
                .Include(p => p.FormGroup)
                .OrderByDescending(p => p.CreatedDate)
                .ToListAsync();
        }

        public async Task<Package> CreateAsync(Package package)
        {
            _context.Packages.Add(package);
            await _context.SaveChangesAsync();
            return package;
        }

        public async Task<bool> UpdateAsync(Package package)
        {
            _context.Packages.Update(package);
            return await _context.SaveChangesAsync() > 0;
        }

        public async Task<bool> DeleteAsync(int packageId)
        {
            var package = await GetByIdAsync(packageId);
            if (package == null) return false;

            _context.Packages.Remove(package);
            return await _context.SaveChangesAsync() > 0;
        }
    }
}