using System;

namespace FormSepeti.Data.Entities
{
    public class AuditLog
    {
        public int LogId { get; set; }
        public int? UserId { get; set; }
        public int? AdminId { get; set; }
        public string Action { get; set; }
        public string EntityType { get; set; }
        public int? EntityId { get; set; }
        public string Details { get; set; }
        public string IpAddress { get; set; }
        public DateTime CreatedDate { get; set; }

        // Navigation properties
        public User User { get; set; }
        public AdminUser AdminUser { get; set; }
    }
}