using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace AppValetParking.Models { 

public class ValetMovimiento
{
    public int Id { get; set; }
    public int? IdRegistro { get; set; }
    public string? Reserva { get; set; }
    public string? Servicio { get; set; }
    public DateTime? FechaHora { get; set; }
    public string? Operador { get; set; }
    public string? MovimientoTexto { get; set; }

        [ForeignKey("IdRegistro")]
        public ValetRegistro? ValetRegistro { get; set; }
    }
}