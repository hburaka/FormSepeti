using System.Collections.Generic;

namespace FormSepeti.Services.Models
{
    public class JotFormSubmissionResult
    {
        public bool Success { get; set; }
        public int UserId { get; set; }
        public int FormId { get; set; }
        public int GroupId { get; set; }
        public string JotFormSubmissionId { get; set; }
        public int? GoogleSheetRowNumber { get; set; }
        public string ErrorMessage { get; set; }
    }

    public class JotFormWebhookPayload
    {
        public string submissionID { get; set; }
        public string formID { get; set; }
        public string ip { get; set; }
        public Dictionary<string, object> rawRequest { get; set; }
    }

    public class JotFormQuestionsResponse
    {
        public Dictionary<string, JotFormQuestion> content { get; set; }
    }

    public class JotFormQuestion
    {
        public string text { get; set; }
        public string type { get; set; }
        public string name { get; set; }
    }

    public class JotFormSubmissionResponse
    {
        public JotFormSubmission content { get; set; }
    }

    public class JotFormSubmission
    {
        public string id { get; set; }
        public string form_id { get; set; }
        public string ip { get; set; }
        public string created_at { get; set; }
        public Dictionary<string, object> answers { get; set; }
    }
}