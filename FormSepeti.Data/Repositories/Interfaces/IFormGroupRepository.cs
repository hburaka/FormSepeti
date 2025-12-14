using System.Collections.Generic;
using System.Threading.Tasks;
using FormSepeti.Data.Entities;

namespace FormSepeti.Data.Repositories.Interfaces
{
    public interface IFormGroupRepository
    {
        Task<FormGroup> GetByIdAsync(int groupId);
        Task<List<FormGroup>> GetAllActiveAsync();
        Task<List<FormGroup>> GetGroupsWithFreeFormsAsync();
        Task<FormGroup> CreateAsync(FormGroup group);
        Task<bool> UpdateAsync(FormGroup group);
        Task<bool> DeleteAsync(int groupId);
    }
}