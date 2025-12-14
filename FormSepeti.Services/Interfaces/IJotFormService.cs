using System.Collections.Generic;
using System.Threading.Tasks;
using FormSepeti.Services.Models;

namespace FormSepeti.Services.Interfaces
{
    public interface IJotFormService
    {
        Task<JotFormSubmissionResult> ProcessWebhook(string rawJson, int userId, int formId, int groupId);
        Task<List<string>> GetFormFields(string jotFormId);
        Task<JotFormSubmission> GetSubmission(string jotFormId, string submissionId);
    }
}