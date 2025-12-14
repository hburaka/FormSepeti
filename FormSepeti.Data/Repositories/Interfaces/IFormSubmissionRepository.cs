using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FormSepeti.Data.Entities;

namespace FormSepeti.Data.Repositories.Interfaces
{
    public interface IFormSubmissionRepository
    {
        Task<FormSubmission> GetByIdAsync(int submissionId);
        Task<FormSubmission> CreateAsync(FormSubmission submission);
        Task<List<FormSubmission>> GetByUserIdAsync(int userId);
        Task<List<FormSubmission>> GetByUserAndFormIdAsync(int userId, int formId);
        Task<List<FormSubmission>> GetRecentByUserIdAsync(int userId, int count);
        Task<int> GetCountByUserIdAsync(int userId);
        Task<int> GetCountByUserIdThisMonthAsync(int userId);
        Task<int> GetCountByUserIdTodayAsync(int userId);
        Task<Dictionary<string, int>> GetSubmissionCountByGroupAsync(int userId);
        Task<Dictionary<string, int>> GetSubmissionCountByMonthAsync(int userId, int months);
    }
}