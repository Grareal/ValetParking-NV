using AppValetParking.Data;
using AppValetParking.Filters;
using AppValetParking.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AppValetParking.Controllers;

[Permiso("TI")]
public class ConfiguracionAccesosController : Controller
{
    private readonly ApplicationDbContext _context;
    public ConfiguracionAccesosController(ApplicationDbContext context) => _context = context;

    public async Task<IActionResult> Index() => View(await _context.VistasSistema.AsNoTracking()
        .Include(v => v.Roles).OrderBy(v => v.Orden).ToListAsync());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Guardar(int[] ids, string[] titulos, string[] iconos,
        string[] urls, int[] ordenes, int[] activos, int[] menus, int[] operadora)
    {
        var vistas = await _context.VistasSistema.Include(v => v.Roles).ToListAsync();
        for (var i = 0; i < ids.Length; i++)
        {
            var vista = vistas.SingleOrDefault(v => v.Id == ids[i]);
            if (vista == null) continue;
            vista.Titulo = titulos.ElementAtOrDefault(i)?.Trim() ?? vista.Titulo;
            vista.Icono = iconos.ElementAtOrDefault(i)?.Trim() ?? "*";
            vista.Url = urls.ElementAtOrDefault(i)?.Trim() ?? vista.Url;
            vista.Orden = ordenes.ElementAtOrDefault(i);
            vista.Activo = activos.Contains(vista.Id);
            vista.MostrarEnMenu = menus.Contains(vista.Id);
            var asignacion = vista.Roles.SingleOrDefault(r => r.Rol == "OperadoraValet");
            if (operadora.Contains(vista.Id) && asignacion == null)
                vista.Roles.Add(new RolVista { Rol = "OperadoraValet", VistaSistemaId = vista.Id });
            else if (!operadora.Contains(vista.Id) && asignacion != null)
                _context.RolVistas.Remove(asignacion);
        }
        await _context.SaveChangesAsync();
        TempData["Ok"] = "Accesos actualizados correctamente.";
        return RedirectToAction(nameof(Index));
    }
}
