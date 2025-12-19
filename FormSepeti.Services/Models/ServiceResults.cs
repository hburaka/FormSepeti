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
        
        // ✅ Kolaylık özellikleri - Razor view'da direkt erişim için
        public int Id => Form?.FormId ?? 0;
        public string Title => Form?.FormName ?? string.Empty;
        public string Description => Form?.FormDescription ?? string.Empty;
        public int SubmissionCount { get; set; } = 0;
    }
}