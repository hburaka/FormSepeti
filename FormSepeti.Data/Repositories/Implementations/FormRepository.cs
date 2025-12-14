using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using FormSepeti.Data.Entities;
using FormSepeti.Data.Repositories.Interfaces;

namespace FormSepeti.Data.Repositories.Implementations
{
    public class FormRepository : IFormRepository
    {
        private readonly ApplicationDbContext _context;

        public FormRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<Form> GetByIdAsync(int formId)
        {
            return await _context.Forms.FindAsync(formId);
        }

        public async Task<Form> GetByJotFormIdAsync(string jotFormId)
        {
            return await _context.Forms
                .FirstOrDefaultAsync(f => f.JotFormId == jotFormId);
        }

        public async Task<List<Form>> GetByGroupIdAsync(int groupId)
        {
            return await _context.FormGroupMappings
                .Where(m => m.GroupId == groupId)
                .Include(m => m.Form)
                .Select(m => m.Form)
                .ToListAsync();
        }

        public async Task<List<Form>> GetAllActiveAsync()
        {
            return await _context.Forms
                .Where(f => f.IsActive)
                .ToListAsync();
        }

        public async Task<Form> CreateAsync(Form form)
        {
            _context.Forms.Add(form);
            await _context.SaveChangesAsync();
            return form;
        }

        public async Task<bool> UpdateAsync(Form form)
        {
            _context.Forms.Update(form);
            return await _context.SaveChangesAsync() > 0;
        }

        public async Task<bool> DeleteAsync(int formId)
        {
            var form = await GetByIdAsync(formId);
            if (form == null) return false;

            _context.Forms.Remove(form);
            return await _context.SaveChangesAsync() > 0;
        }
    }
}