public class TicketEnviado
{
    public int Id { get; set; }

    public string? Folio { get; set; }
    public string? Nombre { get; set; }
    public string? Habitacion { get; set; }
    public string? Hotel { get; set; }
    public string? Tipo { get; set; }
    public string? Comentario { get; set; }
    public string? Impresora { get; set; }

    public DateTime FechaEnvio { get; set; }
}