using System;

namespace FormSepeti.Data.Entities
{
    public class FormSubmission
    {
        public int SubmissionId { get; set; }
        public int UserId { get; set; }
        public int FormId { get; set; }
        public int GroupId { get; set; }
        public string JotFormSubmissionId { get; set; }
        public int? GoogleSheetRowNumber { get; set; }
        public DateTime SubmittedDate { get; set; }
        public string Status { get; set; }
        public string ErrorMessage { get; set; }

        // Navigation properties
        public User User { get; set; }
        public Form Form { get; set; }
        public FormGroup FormGroup { get; set; }
    }
}