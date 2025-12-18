using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Text;
using System.Text.Json;
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
        private readonly IGoogleSheetsService _googleSheetsService; // ✅ EKLENDİ
        private readonly IConfiguration _config;
        private readonly ILogger<WebhookController> _logger;

        public WebhookController(
            IJotFormService jotFormService,
            IFormRepository formRepository,
            IGoogleSheetsService googleSheetsService, // ✅ EKLENDİ
            IConfiguration config,
            ILogger<WebhookController> logger)
        {
            _jotFormService = jotFormService;
            _formRepository = formRepository;
            _googleSheetsService = googleSheetsService; // ✅ EKLENDİ
            _config = config;
            _logger = logger;
        }

        [HttpPost("jotform/{userId}/{formId}/{groupId}")]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> JotFormWebhook(int userId, int formId, int groupId, [FromQuery] string secret = "")
        {
            try
            {
                var expected = _config["JotForm:WebhookSecret"];
                if (string.IsNullOrEmpty(expected) || expected != secret)
                {
                    _logger.LogWarning($"Webhook secret mismatch");
                    return Forbid();
                }

                var form = await Request.ReadFormAsync();
                
                _logger.LogInformation($"📩 Webhook received - Keys: {string.Join(", ", form.Keys)}");

                // ✅ rawRequest'i parse et
                string rawRequestJson = form.ContainsKey("rawRequest") ? form["rawRequest"].ToString() : "{}";
                
                _logger.LogInformation($"🔍 rawRequest: {rawRequestJson.Substring(0, Math.Min(300, rawRequestJson.Length))}");

                // ✅ JotFormService için JSON payload oluştur
                var webhookPayload = new
                {
                    submissionID = form.ContainsKey("submissionID") ? form["submissionID"].ToString() : "",
                    formID = formId.ToString(),
                    rawRequest = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(rawRequestJson)
                };

                var rawJson = System.Text.Json.JsonSerializer.Serialize(webhookPayload);

                _logger.LogInformation($"🚀 Processing webhook...");

                var result = await _jotFormService.ProcessWebhook(rawJson, userId, formId, groupId);

                if (result.Success)
                {
                    _logger.LogInformation($"✅ SUCCESS! Row:{result.GoogleSheetRowNumber}");
                    return Ok(new { success = true, rowNumber = result.GoogleSheetRowNumber });
                }
                else
                {
                    _logger.LogError($"❌ FAILED: {result.ErrorMessage}");
                    return StatusCode(500, new { success = false, error = result.ErrorMessage });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "💥 EXCEPTION in webhook");
                return StatusCode(500, new { error = ex.Message, stackTrace = ex.StackTrace });
            }
        }

        // Alternative: read userId/formId/groupId from payload (hidden fields) if not in route
        [HttpPost("jotform")]
        public async Task<IActionResult> JotFormWebhookGeneric([FromQuery] string secret = "")
        {
            try
            {
                var expected = _config["JotForm:WebhookSecret"];
                if (string.IsNullOrEmpty(expected) || expected != secret)
                {
                    _logger.LogWarning("Webhook secret mismatch");
                    return Forbid();
                }

                string rawJson;
                using (var reader = new StreamReader(Request.Body, Encoding.UTF8))
                    rawJson = await reader.ReadToEndAsync();

                if (string.IsNullOrEmpty(rawJson))
                    return BadRequest(new { error = "Empty payload" });

                // Parse payload to extract userId, formId, groupId from hidden fields
                var doc = JsonDocument.Parse(rawJson);
                var root = doc.RootElement;

                int userId = 0, formId = 0, groupId = 0;

                if (root.TryGetProperty("rawRequest", out var rawReq))
                {
                    foreach (var prop in rawReq.EnumerateObject())
                    {
                        if (prop.Name.EndsWith("userId", StringComparison.OrdinalIgnoreCase))
                            int.TryParse(prop.Value.GetString(), out userId);
                        else if (prop.Name.EndsWith("formId", StringComparison.OrdinalIgnoreCase))
                            int.TryParse(prop.Value.GetString(), out formId);
                        else if (prop.Name.EndsWith("groupId", StringComparison.OrdinalIgnoreCase))
                            int.TryParse(prop.Value.GetString(), out groupId);
                    }
                }

                if (userId == 0 || formId == 0 || groupId == 0)
                {
                    _logger.LogWarning("Missing userId/formId/groupId in payload");
                    return BadRequest(new { error = "Missing userId/formId/groupId in payload" });
                }

                _logger.LogInformation($"JotForm webhook (generic) received for User:{userId}, Form:{formId}, Group:{groupId}");

                var result = await _jotFormService.ProcessWebhook(rawJson, userId, formId, groupId);

                if (result.Success)
                {
                    _logger.LogInformation($"Webhook processed successfully. Submission:{result.JotFormSubmissionId}, Row:{result.GoogleSheetRowNumber}");
                    return Ok(new { success = true, submissionId = result.JotFormSubmissionId, rowNumber = result.GoogleSheetRowNumber });
                }
                else
                {
                    _logger.LogError($"Webhook processing failed: {result.ErrorMessage}");
                    return StatusCode(500, new { success = false, error = result.ErrorMessage });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception in JotForm generic webhook handler");
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
                    ""formID"": """ + formId + @""",
                    ""ip"": ""127.0.0.1"",
                    ""rawRequest"": {
                        ""q1_name"": ""Test User"",
                        ""q2_email"": ""test@example.com"",
                        ""q3_phone"": ""555-1234"",
                        ""q4_message"": ""This is a test submission"",
                        ""q5_userId"": """ + userId + @""",
                        ""q6_formId"": """ + formId + @""",
                        ""q7_groupId"": """ + groupId + @"""
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

                var webhookUrl = $"{Request.Scheme}://{Request.Host}/api/webhook/jotform/{userId}/{formId}/{{groupId}}?secret=9oq8r838ihaq";

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

        [HttpGet("create-sheet/{userId}/{groupId}")]
        public async Task<IActionResult> CreateSheet(int userId, int groupId)
        {
            try
            {
                var url = await _googleSheetsService.CreateSpreadsheetForUserGroup(userId, groupId, $"Test Grup {groupId}");
                return Ok(new { success = true, spreadsheetUrl = url });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, error = ex.Message, stackTrace = ex.StackTrace });
            }
        }

        [HttpGet("create-tab/{userId}/{groupId}/{formId}")]
        public async Task<IActionResult> CreateTab(int userId, int groupId, int formId)
        {
            try
            {
                var form = await _formRepository.GetByIdAsync(formId);
                if (form == null) return NotFound();

                var headers = new List<string> { "name", "email", "phone", "message" };
                var sheetName = form.GoogleSheetName ?? form.FormName;

                var success = await _googleSheetsService.CreateSheetTabForForm(userId, groupId, sheetName, headers);
                
                return Ok(new { success, sheetName, headers });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, error = ex.Message, stackTrace = ex.StackTrace });
            }
        }

        [HttpGet("debug-form/{formId}")]
        public async Task<IActionResult> DebugForm(int formId)
        {
            try
            {
                var form = await _formRepository.GetByIdAsync(formId);
                if (form == null) return NotFound();

                return Ok(new
                {
                    formId = form.FormId,
                    formName = form.FormName,
                    googleSheetName = form.GoogleSheetName,
                    jotFormId = form.JotFormId,
                    isActive = form.IsActive,
                    calculatedSheetName = form.GoogleSheetName ?? form.FormName
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }
    }
}