using System.ComponentModel.DataAnnotations.Schema;

namespace AppValetParking.Models
    {
        public class Usuario
        {
            public required int Id { get; set; }
            public required string  Username { get; set; }
            public required string  Password { get; set; }
        public required string Funciones { get; set; }

        [Column("gaffete")]
        public string? Gafete { get; set; }

        // Nombre real del empleado (ej. "JUAN PEREZ"). Se captura al crear el
        // usuario desde /Usuarios/Crear (buscador de empleado). La app Flutter lo
        // usa para el saludo "Buen turno, <nombre>" en vez del username (oth53226).
        public string? Nombre { get; set; }


    }
}
