using System;

namespace FormSepeti.Data.Entities
{
    public class UserPackage
    {
        public int UserPackageId { get; set; }
        public int UserId { get; set; }
        public int PackageId { get; set; }
        public int GroupId { get; set; }
        public DateTime PurchaseDate { get; set; }
        public DateTime? ActivationDate { get; set; }
        public DateTime? ExpiryDate { get; set; }
        public bool IsActive { get; set; }
        public string PaymentTransactionId { get; set; }
        public decimal PaymentAmount { get; set; }

        // Navigation properties
        public User User { get; set; }
        public Package Package { get; set; }
        public FormGroup FormGroup { get; set; }
    }
}