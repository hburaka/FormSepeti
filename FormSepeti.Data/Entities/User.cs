using System;

namespace FormSepeti.Data.Entities
{
    public class User
    {
        public int UserId { get; set; }
        public string Email { get; set; }
        public string PhoneNumber { get; set; }
        public string PasswordHash { get; set; }
        public bool IsActivated { get; set; }
        public string ActivationToken { get; set; }
        public DateTime? ActivationTokenExpiry { get; set; }
        public string GoogleRefreshToken { get; set; }
        public string GoogleAccessToken { get; set; }
        public DateTime? GoogleTokenExpiry { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime? LastLoginDate { get; set; }
        public bool IsActive { get; set; }
    }
}