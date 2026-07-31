using System;

namespace AppValetParking.Models
{
    /// Código propio (QR o escrito) para cerrar ciclos de estacionado, vinculado
    /// a un nombre. Cuando se usa para confirmar, ese Nombre queda en movimientos
    /// como quien aprobó. Alternativa a validar contra el padrón de empleados.
    public class CodigoLiberacion
    {
        public int Id { get; set; }
        public string Codigo { get; set; } = string.Empty;
        public string Nombre { get; set; } = string.Empty;
        // Código de operador (gafete/ID_ICLASS) del usuario que creó el código.
        public string? CodigoOperador { get; set; }
        public bool Activo { get; set; } = true;
        public string? CreadoPor { get; set; }
        public DateTime Fecha { get; set; } = DateTime.Now;

        // Vigencia: hasta cuándo el código es válido. null = sin caducidad.
        // Si ExpiraEn ya pasó, el código se considera "Caducado" y no valida.
        public DateTime? ExpiraEn { get; set; }
    }
}
