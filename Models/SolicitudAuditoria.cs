namespace AppValetParking.Models;

public class SolicitudAuditoria
{
    public int Id { get; set; }
    public int SolicitudId { get; set; }
    public DateTime Fecha { get; set; } = DateTime.Now;
    public required string Accion { get; set; }
    public string? ActorGafete { get; set; }
    public string? ActorNombre { get; set; }
    public string? Motivo { get; set; }
    public string? ValorAnterior { get; set; }
    public string? ValorNuevo { get; set; }
}
