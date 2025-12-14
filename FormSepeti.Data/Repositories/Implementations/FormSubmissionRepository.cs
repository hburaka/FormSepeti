using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using FormSepeti.Data.Entities;
using FormSepeti.Data.Repositories.Interfaces;

namespace FormSepeti.Data.Repositories.Implementations
{
    public class FormSubmissionRepository : IFormSubmissionRepository
    {
        private readonly ApplicationDbContext _context;

        public FormSubmissionRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<FormSubmission> GetByIdAsync(int submissionId)
        {
            return await _context.FormSubmissions.FindAsync(submissionId);
        }

        public async Task<FormSubmission> CreateAsync(FormSubmission submission)
        {
            _context.FormSubmissions.Add(submission);
            await _context.SaveChangesAsync();
            return submission;
        }

        public async Task<List<FormSubmission>> GetByUserIdAsync(int userId)
        {
            return await _context.FormSubmissions
                .Where(s => s.UserId == userId)
                .Include(s => s.Form)
                .Include(s => s.FormGroup)
                .OrderByDescending(s => s.SubmittedDate)
                .ToListAsync();
        }

        public async Task<List<FormSubmission>> GetByUserAndFormIdAsync(int userId, int formId)
        {
            return await _context.FormSubmissions
                .Where(s => s.UserId == userId && s.FormId == formId)
                .Include(s => s.Form)
                .OrderByDescending(s => s.SubmittedDate)
                .ToListAsync();
        }

        public async Task<List<FormSubmission>> GetRecentByUserIdAsync(int userId, int count)
        {
            return await _context.FormSubmissions
                .Where(s => s.UserId == userId)
                .OrderByDescending(s => s.SubmittedDate)
                .Take(count)
                .Include(s => s.Form)
                .ToListAsync();
        }

        public async Task<int> GetCountByUserIdAsync(int userId)
        {
            return await _context.FormSubmissions
                .Where(s => s.UserId == userId)
                .CountAsync();
        }

        public async Task<int> GetCountByUserIdThisMonthAsync(int userId)
        {
            var startOfMonth = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);
            return await _context.FormSubmissions
                .Where(s => s.UserId == userId && s.SubmittedDate >= startOfMonth)
                .CountAsync();
        }

        public async Task<int> GetCountByUserIdTodayAsync(int userId)
        {
            var today = DateTime.UtcNow.Date;
            return await _context.FormSubmissions
                .Where(s => s.UserId == userId && s.SubmittedDate >= today)
                .CountAsync();
        }

        public async Task<Dictionary<string, int>> GetSubmissionCountByGroupAsync(int userId)
        {
            return await _context.FormSubmissions
                .Where(s => s.UserId == userId)
                .Include(s => s.FormGroup)
                .GroupBy(s => s.FormGroup.GroupName)
                .Select(g => new { GroupName = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.GroupName, x => x.Count);
        }

        public async Task<Dictionary<string, int>> GetSubmissionCountByMonthAsync(int userId, int months)
        {
            var startDate = DateTime.UtcNow.AddMonths(-months);
            var submissions = await _context.FormSubmissions
                .Where(s => s.UserId == userId && s.SubmittedDate >= startDate)
                .ToListAsync();

            return submissions
                .GroupBy(s => s.SubmittedDate.ToString("yyyy-MM"))
                .OrderBy(g => g.Key)
                .ToDictionary(g => g.Key, g => g.Count());
        }
    }
}