using FormSepeti.Data.Entities;

namespace FormSepeti.Services.Models
{
    public class UserRegistrationResult
    {
        public bool Success { get; set; }
        public int UserId { get; set; }
        public string Message { get; set; }
        public string ErrorMessage { get; set; }
    }

    public class PackageAccessInfo
    {
        public bool HasAccess { get; set; }
        public bool RequiresPackage { get; set; }
        public bool IsFree { get; set; }
        public string Message { get; set; }
    }

    public class FormWithAccessInfo
    {
        public Form Form { get; set; }
        public bool HasAccess { get; set; }
        public bool IsFree { get; set; }
        public bool RequiresPackage { get; set; }
    }
}