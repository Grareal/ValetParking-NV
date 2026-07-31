using AppValetParking.Data;
using AppValetParking.Models;
using Microsoft.EntityFrameworkCore;

namespace AppValetParking.Services;

public class AccesoVistasService
{
    private readonly ApplicationDbContext _context;

    public AccesoVistasService(ApplicationDbContext context) => _context = context;

    public static string[] SepararRoles(string? roles) =>
        (roles ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    public static bool EsSuperusuario(IEnumerable<string> roles) =>
        roles.Contains("TI", StringComparer.OrdinalIgnoreCase) ||
        roles.Contains("Administracion", StringComparer.OrdinalIgnoreCase);

    public async Task<List<VistaSistema>> ObtenerMenuAsync(string? rolesCsv)
    {
        var roles = SepararRoles(rolesCsv);
        var query = _context.VistasSistema.AsNoTracking().Where(v => v.Activo && v.MostrarEnMenu);

        if (!EsSuperusuario(roles))
            query = query.Where(v => v.Roles.Any(rv => roles.Contains(rv.Rol)));

        return await query.OrderBy(v => v.Orden).ThenBy(v => v.Titulo).ToListAsync();
    }

    public async Task<bool> PuedeAccederAsync(string path, string? rolesCsv)
    {
        var roles = SepararRoles(rolesCsv);
        if (EsSuperusuario(roles)) return true;

        path = path.TrimEnd('/');
        var vista = await _context.VistasSistema.AsNoTracking()
            .Where(v => v.Activo && v.Url == path)
            .Select(v => new { Roles = v.Roles.Select(rv => rv.Rol).ToList() })
            .SingleOrDefaultAsync();

        // Las rutas que no forman parte del catalogo conservan su comportamiento actual.
        return vista == null || vista.Roles.Any(rol => roles.Contains(rol, StringComparer.OrdinalIgnoreCase));
    }
}
