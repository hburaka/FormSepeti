using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using FormSepeti.Data.Entities;
using FormSepeti.Data.Repositories.Interfaces;

namespace FormSepeti.Data.Repositories.Implementations
{
    public class FormGroupMappingRepository : IFormGroupMappingRepository
    {
        private readonly ApplicationDbContext _context;

        public FormGroupMappingRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<FormGroupMapping> GetByFormAndGroupAsync(int formId, int groupId)
        {
            return await _context.FormGroupMappings
                .Include(m => m.Form)
                .Include(m => m.FormGroup)
                .FirstOrDefaultAsync(m => m.FormId == formId && m.GroupId == groupId);
        }

        public async Task<List<FormGroupMapping>> GetByGroupIdAsync(int groupId)
        {
            return await _context.FormGroupMappings
                .Where(m => m.GroupId == groupId)
                .Include(m => m.Form)
                .Include(m => m.FormGroup)
                .OrderBy(m => m.SortOrder)
                .ToListAsync();
        }

        public async Task<List<FormGroupMapping>> GetByFormIdAsync(int formId)
        {
            return await _context.FormGroupMappings
                .Where(m => m.FormId == formId)
                .Include(m => m.Form)
                .Include(m => m.FormGroup)
                .ToListAsync();
        }

        public async Task<FormGroupMapping> CreateAsync(FormGroupMapping mapping)
        {
            _context.FormGroupMappings.Add(mapping);
            await _context.SaveChangesAsync();
            return mapping;
        }

        public async Task<bool> UpdateAsync(FormGroupMapping mapping)
        {
            _context.FormGroupMappings.Update(mapping);
            return await _context.SaveChangesAsync() > 0;
        }

        public async Task<bool> DeleteAsync(int mappingId)
        {
            var mapping = await _context.FormGroupMappings.FindAsync(mappingId);
            if (mapping == null) return false;

            _context.FormGroupMappings.Remove(mapping);
            return await _context.SaveChangesAsync() > 0;
        }
    }
}