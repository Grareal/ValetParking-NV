namespace AppValetParking.Models
{
    public class ValetRegistro
    {
        public int Id { get; set; }
        public DateTime Fecha { get; set; }  

        public string? Operacion { get; set; }
        public string? NumeroOperador { get; set; }

        public TimeSpan? Solicitud { get; set; }
        public string? Habitacion { get; set; }
        public string? Hotel { get; set; }
        public string? FolioVP { get; set; }
        public string? Valet { get; set; }
        public string? Servicio { get; set; }
        public TimeSpan? HoraSalida { get; set; }

        public string? CajonBuffer { get; set; }
        public string? Estatus { get; set; }
        public string? Reserva { get; set; }
        public string? NombreReserva { get; set; }
        public string? HOSTNAME { get; set; }
        public string? Movimientos { get; set; }



    }
}
