namespace FormSepeti.Data.Entities
{
    public class FormGroupMapping
    {
        public int MappingId { get; set; }
        public int FormId { get; set; }
        public int GroupId { get; set; }
        public bool IsFreeInGroup { get; set; }
        public bool RequiresPackage { get; set; }
        public int SortOrder { get; set; }

        // Navigation properties
        public Form Form { get; set; }
        public FormGroup FormGroup { get; set; }
    }
}