namespace AppValetParking.Models
{
    public class Cajon
    {
        public int Id { get; set; }
        public string Numero { get; set; } = "";
        public bool Ocupado { get; set; }
        public string Ubicacion { get; set; } = "ANDENES";
    }
}
