using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FormSepeti.Data.Entities;
using FormSepeti.Data.Repositories.Interfaces;
using Microsoft.Extensions.Logging;

namespace FormSepeti.Web.Areas.Admin.Pages.Groups
{
    [Authorize(Policy = "AdminOnly")]
    public class IndexModel : PageModel
    {
        private readonly IFormGroupRepository _formGroupRepository;
        private readonly IFormGroupMappingRepository _formGroupMappingRepository;
        private readonly IPackageRepository _packageRepository;
        private readonly ILogger<IndexModel> _logger;

        public IndexModel(
            IFormGroupRepository formGroupRepository,
            IFormGroupMappingRepository formGroupMappingRepository,
            IPackageRepository packageRepository,
            ILogger<IndexModel> logger)
        {
            _formGroupRepository = formGroupRepository;
            _formGroupMappingRepository = formGroupMappingRepository;
            _packageRepository = packageRepository;
            _logger = logger;
        }

        public List<GroupViewModel> Groups { get; set; } = new();
        public int TotalGroups { get; set; }
        public int ActiveGroups { get; set; }
        public int InactiveGroups { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? SearchTerm { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? StatusFilter { get; set; }

        public async Task OnGetAsync()
        {
            _logger.LogInformation("Admin viewing groups list - SearchTerm: {SearchTerm}, StatusFilter: {StatusFilter}",
                SearchTerm, StatusFilter);

            var allGroups = await _formGroupRepository.GetAllAsync();
            var filteredGroups = allGroups.AsQueryable();

            if (!string.IsNullOrWhiteSpace(SearchTerm))
            {
                filteredGroups = filteredGroups.Where(g =>
                    g.GroupName.Contains(SearchTerm, StringComparison.OrdinalIgnoreCase) ||
                    (g.Description != null && g.Description.Contains(SearchTerm, StringComparison.OrdinalIgnoreCase)));
            }

            if (StatusFilter == "active")
            {
                filteredGroups = filteredGroups.Where(g => g.IsActive);
            }
            else if (StatusFilter == "inactive")
            {
                filteredGroups = filteredGroups.Where(g => !g.IsActive);
            }

            Groups = new List<GroupViewModel>();
            foreach (var group in filteredGroups.OrderBy(g => g.SortOrder))
            {
                var mappings = await _formGroupMappingRepository.GetByGroupIdAsync(group.GroupId);
                var packages = await _packageRepository.GetByGroupIdAsync(group.GroupId);

                Groups.Add(new GroupViewModel
                {
                    GroupId = group.GroupId,
                    GroupName = group.GroupName,
                    Description = group.Description ?? "-",
                    IconUrl = group.IconUrl ?? "-",
                    SortOrder = group.SortOrder,
                    IsActive = group.IsActive,
                    CreatedDate = group.CreatedDate,
                    FormCount = mappings.Count,
                    PackageCount = packages.Count
                });
            }

            TotalGroups = allGroups.Count;
            ActiveGroups = allGroups.Count(g => g.IsActive);
            InactiveGroups = TotalGroups - ActiveGroups;
        }

        public async Task<IActionResult> OnPostToggleStatusAsync(int groupId)
        {
            _logger.LogInformation("Admin toggling group status - GroupId: {GroupId}", groupId);

            var group = await _formGroupRepository.GetByIdAsync(groupId);
            if (group == null)
            {
                TempData["ErrorMessage"] = "Grup bulunamadý.";
                return RedirectToPage();
            }

            group.IsActive = !group.IsActive;
            await _formGroupRepository.UpdateAsync(group);

            TempData["SuccessMessage"] = $"Grup {(group.IsActive ? "aktif" : "pasif")} edildi.";
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostDeleteAsync(int groupId)
        {
            _logger.LogInformation("Admin deleting group - GroupId: {GroupId}", groupId);

            var group = await _formGroupRepository.GetByIdAsync(groupId);
            if (group == null)
            {
                TempData["ErrorMessage"] = "Grup bulunamadý.";
                return RedirectToPage();
            }

            // Form ve paket kontrolü
            var mappings = await _formGroupMappingRepository.GetByGroupIdAsync(groupId);
            var packages = await _packageRepository.GetByGroupIdAsync(groupId);

            if (mappings.Any() || packages.Any())
            {
                TempData["ErrorMessage"] = $"Bu grup {mappings.Count} form ve {packages.Count} paket içeriyor. Silinemez.";
                return RedirectToPage();
            }

            await _formGroupRepository.DeleteAsync(groupId);

            TempData["SuccessMessage"] = "Grup baþarýyla silindi.";
            return RedirectToPage();
        }

        public class GroupViewModel
        {
            public int GroupId { get; set; }
            public string GroupName { get; set; } = string.Empty;
            public string Description { get; set; } = string.Empty;
            public string IconUrl { get; set; } = string.Empty;
            public int SortOrder { get; set; }
            public bool IsActive { get; set; }
            public DateTime CreatedDate { get; set; }
            public int FormCount { get; set; }
            public int PackageCount { get; set; }
        }
    }
}