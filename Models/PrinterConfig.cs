namespace AppValetParking.Models
{
    public class PrinterConfig
    {
        public string Hostname { get; set; } = string.Empty;
        public string Printers { get; set; } = string.Empty; // Puede contener varios separados por ;
        public string Name { get; set; } = string.Empty;
        public bool PrintGroups { get; set; } = false;
    }
}
