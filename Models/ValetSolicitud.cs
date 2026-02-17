using System;

namespace AppValetParking.Models
{
    public class ValetSolicitud
    {
        public int Id { get; set; }

        // Campos obligatorios con valor por defecto
        public string? FolioVP { get; set; }
        public string? Destino { get; set; }
        public string? Resort { get; set; }
        public string? Habitacion { get; set; }
        public string? NombreReserva { get; set; }
        public string? NombreSolicitante { get; set; }
        public string? TipoSalida { get; set; }

        public string? ApellidoSolicitante { get; set; }
        public string? Telefono { get; set; }
        public string? Correo { get; set; }
        public string? MarcaVehiculo { get; set; }
        public string? ColorVehiculo { get; set; }
        public string? Comentarios { get; set; }


        // Fechas
        public DateTime FechaSolicitud { get; set; } = DateTime.Now;
        public DateTime TiempoCreado { get; set; } = DateTime.Now;
        public DateTime? TiempoAtendido { get; set; } = null;
    }
}
