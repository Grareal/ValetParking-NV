using System.ComponentModel.DataAnnotations;

namespace AppValetParking.Models;

public class RolVista
{
    public int Id { get; set; }

    [MaxLength(40)]
    public required string Rol { get; set; }

    public int VistaSistemaId { get; set; }
    public VistaSistema? VistaSistema { get; set; }
}
