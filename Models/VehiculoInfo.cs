namespace AppValetParking.Models
{
    public class VehiculoInfo
    {
        public int Id { get; set; }

        public string FolioVP { get; set; }

        public string? Placas { get; set; }

        public string? Modelo { get; set; }

        public string? Color { get; set; }

        public string? Marca { get; set; }

        public DateTime FechaRegistro { get; set; }

        public string Estatus { get; set; } = "Dentro";

    }
}