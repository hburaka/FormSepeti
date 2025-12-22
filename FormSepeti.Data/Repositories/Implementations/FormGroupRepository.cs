using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using FormSepeti.Data.Entities;
using FormSepeti.Data.Repositories.Interfaces;

namespace FormSepeti.Data.Repositories.Implementations
{
    public class FormGroupRepository : IFormGroupRepository
    {
        private readonly ApplicationDbContext _context;

        public FormGroupRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<FormGroup> GetByIdAsync(int groupId)
        {
            return await _context.FormGroups.FindAsync(groupId);
        }

        public async Task<List<FormGroup>> GetAllAsync()
        {
            return await _context.FormGroups
                .OrderBy(g => g.SortOrder)
                .ToListAsync();
        }

        public async Task<List<FormGroup>> GetAllActiveAsync()
        {
            return await _context.FormGroups
                .Where(g => g.IsActive)
                .OrderBy(g => g.SortOrder)
                .ToListAsync();
        }

        public async Task<List<FormGroup>> GetGroupsWithFreeFormsAsync()
        {
            return await _context.FormGroupMappings
                .Where(m => m.IsFreeInGroup && !m.RequiresPackage)
                .Select(m => m.FormGroup)
                .Distinct()
                .ToListAsync();
        }

        public async Task<FormGroup> CreateAsync(FormGroup group)
        {
            _context.FormGroups.Add(group);
            await _context.SaveChangesAsync();
            return group;
        }

        public async Task<bool> UpdateAsync(FormGroup group)
        {
            _context.FormGroups.Update(group);
            return await _context.SaveChangesAsync() > 0;
        }

        public async Task<bool> DeleteAsync(int groupId)
        {
            var group = await GetByIdAsync(groupId);
            if (group == null) return false;

            _context.FormGroups.Remove(group);
            return await _context.SaveChangesAsync() > 0;
        }
    }
}