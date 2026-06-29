using AppValetParking.Data;
using AppValetParking.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AppValetParking.Filters;

namespace AppValetParking.Controllers
{
    [Permiso("TI")]
    public class UsuariosController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly PegasysDbContext _pegaContext;



        public UsuariosController(
            ApplicationDbContext context,
            PegasysDbContext pegaContext)
        {
            _context = context;
            _pegaContext = pegaContext;
        }



        // LISTA
        public async Task<IActionResult> Index()
        {
            return View(await _context.Usuarios.ToListAsync());
        }

        // CREAR
        public IActionResult Crear()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Crear(Usuario usuario, string[] permisos)
        {
            usuario.Funciones = string.Join(",", permisos);

            _context.Add(usuario);
            await _context.SaveChangesAsync();

            return RedirectToAction("Index");
        }

        // EDITAR
        public async Task<IActionResult> Editar(int id)
        {
            var usuario = await _context.Usuarios.FindAsync(id);

            if (usuario == null)
                return NotFound();

            // Definir roles disponibles
            ViewBag.RolesDisponibles = new string[]
 {
    "Operadora",
    "Botones",
    "Movimientos",
    "PuertaSol",      // Nuevo rol agregado
    "Reportes",       // Nuevo rol agregado
    "Administracion",
    "TI",
    "Configuracion"

    
    // Nuevo rol agregado
 };

            // Roles que ya tiene el usuario
            ViewBag.RolesAsignados = usuario.Funciones?.Split(',') ?? new string[] { };

            return View(usuario);
        }

        [HttpGet]
        public async Task<IActionResult> BuscarEmpleado(string term)
        {
            var empleados = await _pegaContext.VV_TARJETAS_EMPLEADOS
                .Where(x =>
                    x.c_fname.Contains(term) ||
                    x.c_lname.Contains(term) ||
                    x.c_mname.Contains(term))
                .Select(x => new
                {
                    nombre = x.c_fname + " " + x.c_lname + " " + x.c_mname,
                    gafete = x.ID_ICLASS
                })
                .Take(10)
                .ToListAsync();

            return Json(empleados);
        }

        [HttpPost]
        public async Task<IActionResult> Editar(int id, string username, string password, string gafete, string[] permisos)
        {
            // Traer usuario existente de la DB
            var usuarioDb = await _context.Usuarios.FindAsync(id);
            if (usuarioDb == null)
                return NotFound();

            // Actualizar los campos
            usuarioDb.Username = username;
            usuarioDb.Password = password;
            usuarioDb.Gafete = gafete;
            usuarioDb.Funciones = permisos != null ? string.Join(",", permisos) : "";

            // Guardar cambios
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }
        // ELIMINAR
        public async Task<IActionResult> Eliminar(int id)
        {
            var usuario = await _context.Usuarios.FindAsync(id);

            if (usuario == null)
                return NotFound();

            _context.Usuarios.Remove(usuario);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }
    }
}