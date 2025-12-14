using System;

namespace FormSepeti.Data.Entities
{
    public class Package
    {
        public int PackageId { get; set; }
        public int GroupId { get; set; }
        public string PackageName { get; set; }
        public string Description { get; set; }
        public decimal Price { get; set; }
        public int? DurationDays { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedDate { get; set; }

        // Navigation property
        public FormGroup FormGroup { get; set; }
    }
}