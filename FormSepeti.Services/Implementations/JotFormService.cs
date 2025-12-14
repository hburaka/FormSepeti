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
        private readonly string _apiKey;

        public JotFormService(
            HttpClient httpClient,
            IGoogleSheetsService googleSheetsService,
            IFormRepository formRepository,
            IFormSubmissionRepository submissionRepository,
            IUserGoogleSheetsRepository userSheetsRepository,
            IConfiguration configuration)
        {
            _httpClient = httpClient;
            _googleSheetsService = googleSheetsService;
            _formRepository = formRepository;
            _submissionRepository = submissionRepository;
            _userSheetsRepository = userSheetsRepository;
            _apiKey = configuration["JotForm:ApiKey"];
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

                var userSheet = await _userSheetsRepository.GetByUserAndGroupAsync(userId, groupId);
                if (userSheet == null)
                {
                    result.ErrorMessage = "Google Sheet not found for user-group combination";
                    return result;
                }

                var formData = ParseFormData(webhookData.rawRequest);
                var headers = formData.Keys.ToList();

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

                await LogSubmission(userId, formId, groupId, webhookData.submissionID, rowNumber, "Success", null);

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

        public async Task<JotFormSubmission> GetSubmission(string jotFormId, string submissionId)
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
                return null;
            }
        }

        private Dictionary<string, string> ParseFormData(Dictionary<string, object> rawRequest)
        {
            var formData = new Dictionary<string, string>();

            foreach (var kvp in rawRequest)
            {
                if (kvp.Key.StartsWith("q") && kvp.Key.Contains("_"))
                {
                    var fieldName = kvp.Key.Split('_', 2)[1];
                    var value = kvp.Value?.ToString() ?? "";

                    if (kvp.Value is JsonElement element)
                    {
                        if (element.ValueKind == JsonValueKind.Object)
                        {
                            value = FlattenJsonObject(element);
                        }
                        else
                        {
                            value = element.ToString();
                        }
                    }

                    formData[fieldName] = value;
                }
            }

            return formData;
        }

        private string FlattenJsonObject(JsonElement element)
        {
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

        private async Task LogSubmission(int userId, int formId, int groupId, string jotFormSubmissionId,
            int? rowNumber, string status, string errorMessage)
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