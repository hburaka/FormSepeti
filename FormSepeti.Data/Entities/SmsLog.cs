using System;

namespace FormSepeti.Data.Entities
{
    public class SmsLog
    {
        public int LogId { get; set; }
        public int? UserId { get; set; }
        public string PhoneNumber { get; set; }
        public string Message { get; set; }
        public string SmsType { get; set; }
        public DateTime SentDate { get; set; }
        public bool IsSuccess { get; set; }
        public string ErrorMessage { get; set; }

        // Navigation property
        public User User { get; set; }
    }
}