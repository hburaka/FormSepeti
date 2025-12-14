using System;

namespace FormSepeti.Data.Entities
{
    public class UserGoogleSheet
    {
        public int SheetId { get; set; }
        public int UserId { get; set; }
        public int GroupId { get; set; }
        public string SpreadsheetId { get; set; }
        public string SpreadsheetUrl { get; set; }
        public string SheetName { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime? LastUpdatedDate { get; set; }

        // Navigation properties
        public User User { get; set; }
        public FormGroup FormGroup { get; set; }
    }
}