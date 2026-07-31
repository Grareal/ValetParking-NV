using System;

namespace AppValetParking.Models
{
    /// <summary>
    /// Historial de transferencias de folio (etiqueta perdida/dañada). Cuando a
    /// un huésped se le asigna un folio nuevo, se renombra su información al
    /// folio nuevo PERO se conserva aquí el vínculo con el anterior, para que el
    /// folio viejo siga siendo consultable ("no olvidar el anterior").
    /// </summary>
    public class FolioTransferido
    {
        public int Id { get; set; }
        public string FolioAnterior { get; set; } = string.Empty;
        public string FolioNuevo { get; set; } = string.Empty;
        public string? Motivo { get; set; }
        public string? Operador { get; set; }
        public DateTime Fecha { get; set; } = DateTime.Now;
    }
}
