using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System;
using System.Threading.Tasks;
using FormSepeti.Services.Interfaces;

namespace FormSepeti.Web.Pages.Form
{
    public class ViewModel : PageModel
    {
        private readonly IFormService _formService;
        public ViewModel(IFormService formService) => _formService = formService;

        [BindProperty(SupportsGet = true)] public int FormId { get; set; }
        public string FormTitle { get; private set; } = "Form";
        public string WebhookUrl => $"{Request.Scheme}://{Request.Host}/api/webhook/jotform/{{userId}}/{FormId}/{{groupId}}";

        // Added property to satisfy Razor references in the view
        public string SpreadsheetUrl { get; private set; } = string.Empty;

        public string JotFormEmbedHtml { get; private set; } = string.Empty;
        public string JotFormJsUrl { get; private set; } = string.Empty;
        public string JotFormIFrameSrc { get; private set; } = "about:blank";
        public string JotFormIFrameId { get; private set; } = $"JotFormIFrame-{Guid.NewGuid():N}";
        public string JotFormEmbedHandlerUrl { get; private set; } = string.Empty;
        public string JotFormBaseUrl { get; private set; } = string.Empty;

        public async Task<IActionResult> OnGetAsync(int id)
        {
            FormId = id;
            var form = await _formService.GetFormByIdAsync(id);
            if (form == null) return NotFound();

            FormTitle = form.FormName;

            // leave empty by default; populate here if your form entity includes a spreadsheet URL
            // Example: SpreadsheetUrl = form.SpreadsheetUrl ?? string.Empty;

            var embed = (form.JotFormEmbedCode ?? string.Empty).Trim();

            if (!string.IsNullOrEmpty(embed) &&
                (embed.Contains("<iframe", StringComparison.OrdinalIgnoreCase) ||
                 embed.Contains("<script", StringComparison.OrdinalIgnoreCase)))
            {
                JotFormEmbedHtml = embed;
                return Page();
            }

            if (!string.IsNullOrEmpty(embed) &&
                (embed.EndsWith(".js", StringComparison.OrdinalIgnoreCase) ||
                 embed.Contains("/jsform/", StringComparison.OrdinalIgnoreCase)))
            {
                JotFormJsUrl = embed;
                return Page();
            }

            if (!string.IsNullOrEmpty(embed) && Uri.TryCreate(embed, UriKind.Absolute, out var parsed))
            {
                JotFormIFrameSrc = embed;
                JotFormBaseUrl = $"{parsed.Scheme}://{parsed.Host}";
                JotFormEmbedHandlerUrl = $"{JotFormBaseUrl}/s/umd/latest/for-form-embed-handler.js";
                return Page();
            }

            if (form.JotFormId != null)
            {
                var idString = form.JotFormId.ToString();
                if (!string.IsNullOrWhiteSpace(idString))
                {
                    JotFormIFrameSrc = $"https://form.jotform.com/{idString}";
                    JotFormBaseUrl = "https://form.jotform.com";
                    JotFormEmbedHandlerUrl = string.Empty;
                    return Page();
                }
            }

            JotFormIFrameSrc = "about:blank";
            return Page();
        }
    }
}
