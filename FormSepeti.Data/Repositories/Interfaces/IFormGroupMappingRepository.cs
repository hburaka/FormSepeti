using System.Collections.Generic;
using System.Threading.Tasks;
using FormSepeti.Data.Entities;

namespace FormSepeti.Data.Repositories.Interfaces
{
    public interface IFormGroupMappingRepository
    {
        Task<FormGroupMapping> GetByFormAndGroupAsync(int formId, int groupId);
        Task<List<FormGroupMapping>> GetByGroupIdAsync(int groupId);
        Task<List<FormGroupMapping>> GetByFormIdAsync(int formId);
        Task<FormGroupMapping> CreateAsync(FormGroupMapping mapping);
        Task<bool> UpdateAsync(FormGroupMapping mapping);
        Task<bool> DeleteAsync(int mappingId);
    }
}