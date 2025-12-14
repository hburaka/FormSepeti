using System;

namespace FormSepeti.Data.Entities
{
    public class EmailLog
    {
        public int LogId { get; set; }
        public int? UserId { get; set; }
        public string EmailTo { get; set; }
        public string Subject { get; set; }
        public string EmailType { get; set; }
        public DateTime SentDate { get; set; }
        public bool IsSuccess { get; set; }
        public string ErrorMessage { get; set; }

        // Navigation property
        public User User { get; set; }
    }
}