namespace AppValetParking.Models
{
    public class VehiculoFoto
    {
        public int Id { get; set; }

        public string FolioVP { get; set; } = string.Empty;

        public string Slot { get; set; } = string.Empty; // FRENTE, TRASERA, IZQ, DER

        public string RutaArchivo { get; set; } = string.Empty; // ej: /uploads/VPB1234/FRENTE.jpg

        // Comentario opcional del valet sobre la foto (ej. "rayón en puerta").
        public string? Comentario { get; set; }

        public DateTime FechaCreacion { get; set; } = DateTime.Now;
    }
}
