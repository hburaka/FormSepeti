using System.Collections.Generic;
using System.Threading.Tasks;
using FormSepeti.Data.Entities;

namespace FormSepeti.Data.Repositories.Interfaces
{
    public interface IFormRepository
    {
        Task<Form> GetByIdAsync(int formId);
        Task<Form> GetByJotFormIdAsync(string jotFormId);
        Task<List<Form>> GetByGroupIdAsync(int groupId);
        Task<List<Form>> GetAllActiveAsync();
        Task<List<Form>> GetAllAsync(); 
        Task<Form> CreateAsync(Form form);
        Task<bool> UpdateAsync(Form form);
        Task<bool> DeleteAsync(int formId);
    }
}