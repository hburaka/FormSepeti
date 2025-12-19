using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using FormSepeti.Data.Entities;
using FormSepeti.Data.Repositories.Interfaces;
using FormSepeti.Services.Interfaces;
using FormSepeti.Services.Models;

namespace FormSepeti.Services.Implementations
{
    public class JotFormService : IJotFormService
    {
        private readonly HttpClient _httpClient;                    
        private readonly IGoogleSheetsService _googleSheetsService;
        private readonly IFormRepository _formRepository;
        private readonly IFormSubmissionRepository _submissionRepository;
        private readonly IUserGoogleSheetsRepository _userSheetsRepository;
        private readonly IFormGroupRepository _groupRepository;
        private readonly string _apiKey;

        public JotFormService(
            HttpClient httpClient,
            IGoogleSheetsService googleSheetsService,
            IFormRepository formRepository,
            IFormSubmissionRepository submissionRepository,
            IUserGoogleSheetsRepository userSheetsRepository,
            IFormGroupRepository groupRepository,
            IConfiguration configuration)
        {
            _httpClient = httpClient;
            _googleSheetsService = googleSheetsService;
            _formRepository = formRepository;
            _submissionRepository = submissionRepository;
            _userSheetsRepository = userSheetsRepository;
            _groupRepository = groupRepository;
            _apiKey = configuration["JotForm:ApiKey"] ?? throw new InvalidOperationException("JotForm:ApiKey is missing in configuration"); // ✅ FIX
        }

        public async Task<JotFormSubmissionResult> ProcessWebhook(string rawJson, int userId, int formId, int groupId)
        {
            var result = new JotFormSubmissionResult
            {
                Success = false,
                UserId = userId,
                FormId = formId,
                GroupId = groupId
            };

            try
            {
                var webhookData = JsonSerializer.Deserialize<JotFormWebhookPayload>(rawJson);
                if (webhookData?.rawRequest == null)
                {
                    result.ErrorMessage = "Invalid webhook payload";
                    return result;
                }

                result.JotFormSubmissionId = webhookData.submissionID;

                var form = await _formRepository.GetByIdAsync(formId);
                if (form == null)
                {
                    result.ErrorMessage = "Form not found";
                    return result;
                }

                // ✅ SHEET YOKSA OLUŞTUR
                var userSheet = await _userSheetsRepository.GetByUserAndGroupAsync(userId, groupId);
                if (userSheet == null)
                {
                    Console.WriteLine($"⚠️ No Google Sheet found for UserId={userId}, GroupId={groupId}. Creating...");
                    
                    var group = await _groupRepository.GetByIdAsync(groupId);
                    var groupName = group?.GroupName ?? $"Group {groupId}";
                    
                    var spreadsheetUrl = await _googleSheetsService.CreateSpreadsheetForUserGroup(
                        userId, 
                        groupId, 
                        groupName
                    );
                    
                    if (string.IsNullOrEmpty(spreadsheetUrl))
                    {
                        result.ErrorMessage = "Failed to create Google Sheet";
                        return result;
                    }
                    
                    Console.WriteLine($"✅ Created Google Sheet: {spreadsheetUrl}");
                    
                    userSheet = await _userSheetsRepository.GetByUserAndGroupAsync(userId, groupId);
                    
                    if (userSheet == null)
                    {
                        result.ErrorMessage = "Google Sheet created but not found in database";
                        return result;
                    }
                }

                var formData = ParseFormData(webhookData.rawRequest);
                var headers = formData.Keys.OrderBy(k => k).ToList();

                Console.WriteLine($"🔍 Auto-detected {headers.Count} fields: {string.Join(", ", headers)}");

                await _googleSheetsService.CreateSheetTabForForm(
                    userId,
                    groupId,
                    form.GoogleSheetName ?? form.FormName,
                    headers
                );

                var rowNumber = await _googleSheetsService.AppendFormDataToSheet(
                    userId,
                    groupId,
                    form.GoogleSheetName ?? form.FormName,
                    formData
                );

                if (rowNumber == -1)
                {
                    result.ErrorMessage = "Failed to write to Google Sheets";
                    return result;
                }

                result.GoogleSheetRowNumber = rowNumber;
                result.Success = true;

                await LogSubmission(userId, formId, groupId, webhookData.submissionID, rowNumber, "Success", string.Empty); // ✅ FIX: null → string.Empty

                return result;
            }
            catch (Exception ex)
            {
                result.ErrorMessage = ex.Message;
                await LogSubmission(userId, formId, groupId, result.JotFormSubmissionId, null, "Failed", ex.Message);
                return result;
            }
        }

        public async Task<List<string>> GetFormFields(string jotFormId)
        {
            try
            {
                var response = await _httpClient.GetAsync($"form/{jotFormId}/questions?apiKey={_apiKey}");
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync();
                var questionsData = JsonSerializer.Deserialize<JotFormQuestionsResponse>(json);

                var fields = new List<string>();

                if (questionsData?.content != null)
                {
                    foreach (var question in questionsData.content)
                    {
                        if (question.Value?.text != null)
                        {
                            fields.Add(question.Value.text);
                        }
                    }
                }

                return fields;
            }
            catch (Exception)
            {
                return new List<string>();
            }
        }

        public async Task<JotFormSubmission?> GetSubmission(string jotFormId, string submissionId) // ✅ FIX: nullable return
        {
            try
            {
                var response = await _httpClient.GetAsync($"submission/{submissionId}?apiKey={_apiKey}");
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync();
                var submission = JsonSerializer.Deserialize<JotFormSubmissionResponse>(json);

                return submission?.content;
            }
            catch (Exception)
            {
                return null; // ✅ OK: nullable return type
            }
        }

        private Dictionary<string, string> ParseFormData(Dictionary<string, object> rawRequest)
        {
            var formData = new Dictionary<string, string>();

            foreach (var kvp in rawRequest)
            {
                var key = kvp.Key;
                var value = kvp.Value?.ToString() ?? "";

                var metadataFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    "pretty", "ip", "submissionID", "formID", "userId", "formId", "groupId",
                    "action", "event", "documentID", "teamID", "appID", "unread", "parent",
                    "isSilent", "fromTable", "customParams", "customTitle", "customBody",
                    "subject", "product", "webhookURL", "username", "type", "formTitle"
                };

                if (metadataFields.Contains(key))
                {
                    continue;
                }

                if (key.Equals("rawRequest", StringComparison.OrdinalIgnoreCase))
                {
                    if (kvp.Value is JsonElement rawReqElement && rawReqElement.ValueKind == JsonValueKind.Object)
                    {
                        foreach (var field in rawReqElement.EnumerateObject())
                        {
                            var fieldKey = field.Name;
                            var fieldValue = field.Value.ValueKind == JsonValueKind.String 
                                ? field.Value.GetString() ?? "" 
                                : field.Value.ToString();

                            if (fieldKey.StartsWith("q") && fieldKey.Contains("_"))
                            {
                                var parts = fieldKey.Split('_', 2);
                                if (parts.Length == 2)
                                {
                                    fieldKey = parts[1];
                                }
                            }

                            if (field.Value.ValueKind == JsonValueKind.Object)
                            {
                                fieldValue = FlattenJsonObject(field.Value);
                            }

                            var jotFormInternalFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                            {
                                "slug", "jsExecutionTracker", "submitSource", "submitDate", "buildDate",
                                "uploadServerUrl", "eventObserver", "event_id", "timeToSubmit",
                                "validatedNewRequiredFieldIDs", "path", "userId", "formId", "groupId"
                            };

                            if (!jotFormInternalFields.Contains(fieldKey) && !string.IsNullOrWhiteSpace(fieldValue))
                            {
                                formData[fieldKey] = fieldValue;
                            }
                        }
                    }
                    continue;
                }

                if (kvp.Value is JsonElement element)
                {
                    if (element.ValueKind == JsonValueKind.Object)
                    {
                        value = FlattenJsonObject(element);
                    }
                    else if (element.ValueKind == JsonValueKind.String)
                    {
                        value = element.GetString() ?? "";
                    }
                    else
                    {
                        value = element.ToString();
                    }
                }

                if (!string.IsNullOrWhiteSpace(value))
                {
                    formData[key] = value;
                }
            }

            return formData;
        }

        private string FlattenJsonObject(JsonElement element)
        {
            Console.WriteLine($"📅 FlattenJsonObject called for element with {element.GetRawText()}");
            
            if (element.TryGetProperty("year", out var year) &&
                element.TryGetProperty("month", out var month) &&
                element.TryGetProperty("day", out var day))
            {
                Console.WriteLine($"📅 DateTime detected: year={year}, month={month}, day={day}");
                
                var y = year.GetString() ?? year.ToString();
                var m = month.GetString()?.PadLeft(2, '0') ?? month.ToString().PadLeft(2, '0');
                var d = day.GetString()?.PadLeft(2, '0') ?? day.ToString().PadLeft(2, '0');
                
                if (element.TryGetProperty("hour", out var hour) &&
                    element.TryGetProperty("min", out var min))
                {
                    var h = hour.GetString()?.PadLeft(2, '0') ?? hour.ToString().PadLeft(2, '0');
                    var mi = min.GetString()?.PadLeft(2, '0') ?? min.ToString().PadLeft(2, '0');
                    
                    var formatted = $"{y}-{m}-{d} {h}:{mi}";
                    Console.WriteLine($"📅 Formatted as: {formatted}");
                    return formatted;
                }
                
                var dateFormatted = $"{y}-{m}-{d}";
                Console.WriteLine($"📅 Formatted as: {dateFormatted}");
                return dateFormatted;
            }

            Console.WriteLine($"⚠️ Not a DateTime field, using default join");

            var parts = new List<string>();

            foreach (var property in element.EnumerateObject())
            {
                var value = property.Value.ValueKind == JsonValueKind.String
                    ? property.Value.GetString()
                    : property.Value.ToString();

                if (!string.IsNullOrWhiteSpace(value))
                {
                    parts.Add(value);
                }
            }

            return string.Join(", ", parts);
        }

        private async Task LogSubmission(int userId, int formId, int groupId, string? jotFormSubmissionId,
            int? rowNumber, string status, string? errorMessage) // ✅ FIX: nullable parameters
        {
            var submission = new FormSubmission
            {
                UserId = userId,
                FormId = formId,
                GroupId = groupId,
                JotFormSubmissionId = jotFormSubmissionId,
                GoogleSheetRowNumber = rowNumber,
                SubmittedDate = DateTime.UtcNow,
                Status = status,
                ErrorMessage = errorMessage
            };

            await _submissionRepository.CreateAsync(submission);
        }
    }
}