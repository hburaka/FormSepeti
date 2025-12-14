using System.Collections.Generic;
using System.Threading.Tasks;
using FormSepeti.Data.Entities;
using FormSepeti.Data.Repositories.Interfaces;
using FormSepeti.Services.Interfaces;
using FormSepeti.Services.Models;

namespace FormSepeti.Services.Implementations
{
    public class FormService : IFormService
    {
        private readonly IFormRepository _formRepository;
        private readonly IFormGroupMappingRepository _formGroupMappingRepository;
        private readonly IPackageService _packageService;
        private readonly IFormSubmissionRepository _submissionRepository;

        public FormService(
            IFormRepository formRepository,
            IFormGroupMappingRepository formGroupMappingRepository,
            IPackageService packageService,
            IFormSubmissionRepository submissionRepository)
        {
            _formRepository = formRepository;
            _formGroupMappingRepository = formGroupMappingRepository;
            _packageService = packageService;
            _submissionRepository = submissionRepository;
        }

        public async Task<List<FormWithAccessInfo>> GetFormsByGroupIdAsync(int userId, int groupId)
        {
            var mappings = await _formGroupMappingRepository.GetByGroupIdAsync(groupId);
            var result = new List<FormWithAccessInfo>();

            foreach (var mapping in mappings)
            {
                var accessInfo = await _packageService.GetUserAccessToFormAsync(userId, mapping.FormId, groupId);

                result.Add(new FormWithAccessInfo
                {
                    Form = mapping.Form,
                    HasAccess = accessInfo.HasAccess,
                    IsFree = accessInfo.IsFree,
                    RequiresPackage = accessInfo.RequiresPackage
                });
            }

            return result;
        }

        public async Task<Form> GetFormByIdAsync(int formId)
        {
            return await _formRepository.GetByIdAsync(formId);
        }

        public async Task<List<FormSubmission>> GetSubmissionsByFormIdAsync(int userId, int formId)
        {
            return await _submissionRepository.GetByUserAndFormIdAsync(userId, formId);
        }

        public async Task<List<FormSubmission>> GetAllSubmissionsByUserIdAsync(int userId)
        {
            return await _submissionRepository.GetByUserIdAsync(userId);
        }
    }
}