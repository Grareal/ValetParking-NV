using System;

namespace AppValetParking.Models
{
    public class ValetSolicitud
    {
        public int Id { get; set; }

        // Campos obligatorios con valor por defecto
        public string FolioVP { get; set; } = string.Empty;
        public string Destino { get; set; } = "Lobby";
        public string Resort { get; set; } = "Desconocido";
        public string Habitacion { get; set; } = "N/A";
        public string NombreReserva { get; set; } = "Invitado";
        public string NombreSolicitante { get; set; } = "Desconocido";
        public string TipoSalida { get; set; } = "Normal";

        // Campos opcionales, nunca null
        public string ApellidoSolicitante { get; set; } = string.Empty;
        public string Telefono { get; set; } = string.Empty;
        public string Correo { get; set; } = string.Empty;
        public string MarcaVehiculo { get; set; } = string.Empty;
        public string ColorVehiculo { get; set; } = string.Empty;
        public string Comentarios { get; set; } = string.Empty;

        // Fechas
        public DateTime FechaSolicitud { get; set; } = DateTime.Now;
        public DateTime TiempoCreado { get; set; } = DateTime.Now;
        public DateTime? TiempoAtendido { get; set; } = null;
    }
}
