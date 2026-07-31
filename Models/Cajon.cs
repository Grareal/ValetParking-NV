namespace AppValetParking.Models
{
    public class Cajon
    {
        public int Id { get; set; }
        public string Numero { get; set; } = "";
        public bool Ocupado { get; set; }
        public string Ubicacion { get; set; } = "ANDENES";

        // Señalamiento que queda visible en el Mapa de Cajones tras una
        // liberación forzada (ver CajonesController.LiberarForzado), hasta
        // que el cajón se vuelva a ocupar normalmente (OcupaCajon lo limpia).
        public string? UltimoMotivoLiberacion { get; set; }
        public DateTime? FechaUltimaLiberacion { get; set; }
    }
}
