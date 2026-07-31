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

        public string? Placas { get; set; }
        public string? Marca { get; set; }
        public string? Color { get; set; }
        public string? Posicion { get; set; }
        public string? Estatus { get; set; }

        // ── Lock de la tarea ──────────────────────────────────
        // Quién tomó la solicitud (bloqueo). TomadoPorId es el gaffete/ID_ICLASS
        // del operador; TomadoPor su nombre para mostrar en la app.
        public string? TomadoPor { get; set; }
        public string? TomadoPorId { get; set; }
        public DateTime? FechaTomado { get; set; }

        // Solo PENDIENTE_QR bloquea al valet para tomar otra solicitud.
        public string? EstadoPaso { get; set; }
        public DateTime? FechaPendienteQr { get; set; }

        /// Calculado: true si la solicitud está bloqueada (ya la tomó un valet).
        [System.ComponentModel.DataAnnotations.Schema.NotMapped]
        public bool Bloqueada => !string.IsNullOrEmpty(TomadoPorId);


        // Fechas
        public DateTime FechaSolicitud { get; set; } = DateTime.Now;
        public DateTime TiempoCreado { get; set; } = DateTime.Now;
        public DateTime? TiempoAtendido { get; set; } = null;

        /// Calculado: true cuando Estatus == "Por entregar" (no requiere columna nueva).
        [System.ComponentModel.DataAnnotations.Schema.NotMapped]
        public bool PorEntregar => Estatus?.ToLowerInvariant() == "por entregar";
    }
}
