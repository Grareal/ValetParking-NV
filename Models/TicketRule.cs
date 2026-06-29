namespace AppValetParking.Models
{
    public class TicketRule
    {
        public int Prioridad { get; set; }

        public string NombreRegla { get; set; }

        public string Campo { get; set; }   // "Hotel" o "Tipo"

        public string ContieneTexto { get; set; }

        public string ColorHex { get; set; }

        public string Impresora { get; set; }
    }
}