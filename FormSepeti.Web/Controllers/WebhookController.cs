using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using FormSepeti.Data.Repositories.Interfaces;
using FormSepeti.Services.Interfaces;

namespace FormSepeti.Web.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class WebhookController : ControllerBase
    {
        private readonly IJotFormService _jotFormService;
        private readonly IFormRepository _formRepository;
        private readonly ILogger<WebhookController> _logger;

        public WebhookController(
            IJotFormService jotFormService,
            IFormRepository formRepository,
            ILogger<WebhookController> logger)
        {
            _jotFormService = jotFormService;
            _formRepository = formRepository;
            _logger = logger;
        }

        [HttpPost("jotform/{userId}/{formId}/{groupId}")]
        public async Task<IActionResult> JotFormWebhook(int userId, int formId, int groupId)
        {
            try
            {
                string rawJson;
                using (var reader = new StreamReader(Request.Body, Encoding.UTF8))
                {
                    rawJson = await reader.ReadToEndAsync();
                }

                if (string.IsNullOrEmpty(rawJson))
                {
                    _logger.LogWarning("Empty webhook payload received");
                    return BadRequest(new { error = "Empty payload" });
                }

                _logger.LogInformation($"JotForm webhook received for User:{userId}, Form:{formId}, Group:{groupId}");

                var result = await _jotFormService.ProcessWebhook(rawJson, userId, formId, groupId);

                if (result.Success)
                {
                    _logger.LogInformation($"Webhook processed successfully. Submission:{result.JotFormSubmissionId}, Row:{result.GoogleSheetRowNumber}");

                    return Ok(new
                    {
                        success = true,
                        message = "Form data successfully saved to Google Sheets",
                        submissionId = result.JotFormSubmissionId,
                        rowNumber = result.GoogleSheetRowNumber
                    });
                }
                else
                {
                    _logger.LogError($"Webhook processing failed: {result.ErrorMessage}");

                    return BadRequest(new
                    {
                        success = false,
                        error = result.ErrorMessage
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception in JotForm webhook handler");
                return StatusCode(500, new { error = "Internal server error" });
            }
        }

        [HttpGet("test/{userId}/{formId}/{groupId}")]
        public async Task<IActionResult> TestWebhook(int userId, int formId, int groupId)
        {
            try
            {
                var testPayload = @"{
                    ""submissionID"": ""test-" + Guid.NewGuid().ToString().Substring(0, 8) + @""",
                    ""formID"": ""123456789"",
                    ""ip"": ""127.0.0.1"",
                    ""rawRequest"": {
                        ""q1_name"": ""Test User"",
                        ""q2_email"": ""test@example.com"",
                        ""q3_phone"": ""555-1234"",
                        ""q4_message"": ""This is a test submission""
                    }
                }";

                var result = await _jotFormService.ProcessWebhook(testPayload, userId, formId, groupId);

                return Ok(new
                {
                    success = result.Success,
                    message = result.Success ? "Test successful" : result.ErrorMessage,
                    submissionId = result.JotFormSubmissionId,
                    rowNumber = result.GoogleSheetRowNumber
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpGet("status/{userId}/{formId}")]
        public async Task<IActionResult> WebhookStatus(int userId, int formId)
        {
            try
            {
                var form = await _formRepository.GetByIdAsync(formId);
                if (form == null)
                {
                    return NotFound(new { error = "Form not found" });
                }

                var webhookUrl = $"{Request.Scheme}://{Request.Host}/api/webhook/jotform/{userId}/{formId}/{{groupId}}";

                return Ok(new
                {
                    formId = formId,
                    formName = form.FormName,
                    jotFormId = form.JotFormId,
                    webhookUrl = webhookUrl,
                    instructions = "JotForm'da webhook URL'sini ayarlayın"
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }
    }
}