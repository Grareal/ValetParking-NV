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


    }
}
