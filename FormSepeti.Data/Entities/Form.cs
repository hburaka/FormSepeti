using System;

namespace FormSepeti.Data.Entities
{
    public class Form
    {
        public int FormId { get; set; }
        public string FormName { get; set; }
        public string FormDescription { get; set; }
        public string JotFormId { get; set; }
        public string JotFormEmbedCode { get; set; }
        public string GoogleSheetName { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedDate { get; set; }
    }
}