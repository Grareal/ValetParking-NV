using System.ComponentModel.DataAnnotations;

namespace AppValetParking.Models;

public class VistaSistema
{
    public int Id { get; set; }

    [MaxLength(80)]
    public required string Clave { get; set; }

    [MaxLength(120)]
    public required string Titulo { get; set; }

    [MaxLength(30)]
    public string Icono { get; set; } = "*";

    [MaxLength(250)]
    public required string Url { get; set; }

    public int Orden { get; set; }
    public bool Activo { get; set; } = true;
    public bool MostrarEnMenu { get; set; } = true;

    public ICollection<RolVista> Roles { get; set; } = new List<RolVista>();
}
