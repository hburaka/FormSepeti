using System.Collections.Generic;
using System.Threading.Tasks;
using FormSepeti.Data.Entities;
using FormSepeti.Services.Models;

namespace FormSepeti.Services.Interfaces
{
    public interface IFormService
    {
        Task<List<FormWithAccessInfo>> GetFormsByGroupIdAsync(int userId, int groupId);
        Task<Form> GetFormByIdAsync(int formId);
        Task<List<FormSubmission>> GetSubmissionsByFormIdAsync(int userId, int formId);
        Task<List<FormSubmission>> GetAllSubmissionsByUserIdAsync(int userId);
        Task<int> GetFormGroupIdAsync(int formId);
    }
}