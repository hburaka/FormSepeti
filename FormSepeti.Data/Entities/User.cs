using System;

namespace FormSepeti.Data.Entities
{
    public class User
    {
        // ========================================
        // ✅ KİMLİK VE HESAP BİLGİLERİ
        // ========================================
        public int UserId { get; set; }
        public string? Email { get; set; }
        public string? PhoneNumber { get; set; }
        public string? PasswordHash { get; set; }
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        
        // ========================================
        // ✅ AKTİVASYON BİLGİLERİ
        // ========================================
        public bool IsActivated { get; set; }
        public string? ActivationToken { get; set; }
        public DateTime? ActivationTokenExpiry { get; set; }
        
        // ========================================
        // ✅ GOOGLE OAUTH BİLGİLERİ
        // ========================================
        public string? GoogleRefreshToken { get; set; }
        public string? GoogleAccessToken { get; set; }
        public DateTime? GoogleTokenExpiry { get; set; }
        public string? GoogleId { get; set; }
        public string? ProfilePhotoUrl { get; set; }
        
        // ========================================
        // ✅ SİSTEM BİLGİLERİ
        // ========================================
        public DateTime CreatedDate { get; set; }
        public DateTime? LastLoginDate { get; set; }
        public bool IsActive { get; set; }
        
        // ========================================
        // ✅ FATURA BİLGİLERİ (Paket Satın Alma İçin)
        // ========================================
        
        // Müşteri Tipi
        public CustomerType? CustomerType { get; set; }
        
        // Şahıs Müşteri - Fatura Bilgileri
        public string? BillingFirstName { get; set; }  // Faturadaki ad
        public string? BillingLastName { get; set; }   // Faturadaki soyad
        public string? TCKimlikNo { get; set; }
        
        // Kurumsal Müşteri - Fatura Bilgileri
        public string? CompanyName { get; set; }
        public string? TaxOffice { get; set; }
        public string? TaxNumber { get; set; }
        public string? AuthorizedPersonName { get; set; }
        
        // Adres Bilgileri (Fatura İçin)
        public string? BillingAddress { get; set; }
        public string? BillingCity { get; set; }
        public string? BillingDistrict { get; set; }
        public string? BillingPostalCode { get; set; }
        
        // Fatura Adresi Ayrı mı?
        public bool UseDifferentInvoiceAddress { get; set; }
        public string? InvoiceAddress { get; set; }
        
        // Fatura Bilgileri Tamamlandı mı?
        public bool IsInvoiceInfoComplete { get; set; }
        public DateTime? InvoiceInfoCompletedDate { get; set; }
        
        // ========================================
        // ✅ HELPER PROPERTIES
        // ========================================
        public string FullName => $"{FirstName} {LastName}".Trim();
        public string BillingFullName => $"{BillingFirstName} {BillingLastName}".Trim();
    }
    
    public enum CustomerType
    {
        Individual = 1,  // Şahıs
        Corporate = 2    // Kurumsal
    }
}