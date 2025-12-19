using System;

namespace FormSepeti.Data.Entities
{
    public class AdminUser
    {
        public int AdminId { get; set; }
        public required string Username { get; set; } // ✅ required eklendi
        public required string PasswordHash { get; set; } // ✅ required eklendi
        public required string FullName { get; set; } // ✅ required eklendi
        public required string Email { get; set; } // ✅ required eklendi
        public required string Role { get; set; } // ✅ required eklendi
        public bool IsActive { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime? LastLoginDate { get; set; }
    }
}