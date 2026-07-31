using AppValetParking.Data;
using AppValetParking.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AppValetParking.Filters;
using System.Text.RegularExpressions;

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

        // Catálogo único de los 3 roles del sistema. Se usa en Crear/Editar.
        // Clave = token guardado en Usuario.Funciones (CSV). Etiqueta = texto visible.
        public static readonly (string Clave, string Etiqueta, string Descripcion)[] RolesSistema = new[]
        {
            ("OperadoraValet", "Operadora / Valet", "Operación diaria: solicitudes, registros, reportes y tickets."),
            ("Administracion", "Administradores",  "Acceso total al panel de Valet Parking."),
            ("TI",             "TI",               "Acceso total + configuración de vistas, impresoras y accesos.")
        };

        // LISTA
        public async Task<IActionResult> Index()
        {
            return View("IndexProfesional", await _context.Usuarios.ToListAsync());
        }

        // CREAR
        public IActionResult Crear()
        {
            ViewBag.Roles = RolesSistema;
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Crear(Usuario usuario, string[] permisos)
        {
            usuario.Funciones = string.Join(",", permisos ?? Array.Empty<string>());

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

            ViewBag.Roles = RolesSistema;
            ViewBag.RolesAsignados = usuario.Funciones?.Split(',', StringSplitOptions.RemoveEmptyEntries) ?? Array.Empty<string>();

            return View("EditarProfesional", usuario);
        }

        [HttpPost]
        public async Task<IActionResult> Editar(int id, string username, string password, string gafete, string nombre, string[] permisos)
        {
            var usuarioDb = await _context.Usuarios.FindAsync(id);
            if (usuarioDb == null)
                return NotFound();

            usuarioDb.Username = username;
            usuarioDb.Password = password;
            usuarioDb.Gafete = gafete;
            if (!string.IsNullOrWhiteSpace(nombre))
                usuarioDb.Nombre = nombre;
            usuarioDb.Funciones = permisos != null ? string.Join(",", permisos) : "";

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

        // =====================================================================
        //  BÚSQUEDA DE EMPLEADO EN PEGASYS
        // =====================================================================
        // Nota de datos (vista VV_TARJETAS_EMPLEADOS):
        //   clavenomina -> número de colaborador, con ceros y espacios (ej. "053226")
        //   emp         -> prefijo de compañía del usuario de red (ej. "OTH")
        //   c_fname     -> usuario de red crudo con ceros (ej. "OTH053226")  ¡NO es el nombre!
        //   c_lname + c_mname -> nombre real (ej. "MEZA BAUTISTA" + "LUIS")
        //   ID_ICLASS   -> gafete (ej. "32993")
        // Regla de mapeo: usuario = prefijo + clave SIN ceros a la izquierda (OTH53226).

        /// <summary>Buscador principal: por número de colaborador.</summary>
        [HttpGet]
        public async Task<IActionResult> BuscarPorColaborador(string numero)
        {
            if (string.IsNullOrWhiteSpace(numero))
                return Json(Array.Empty<object>());

            numero = numero.Trim();
            var numeroLimpio = numero.TrimStart('0');
            if (numeroLimpio.Length == 0) numeroLimpio = "0";

            // La vista guarda la clave con padding; traemos candidatos y filtramos
            // en memoria comparando la clave normalizada (evita falsos positivos con ID_ICLASS).
            var candidatos = await _pegaContext.VV_TARJETAS_EMPLEADOS
                .Where(e => e.clavenomina != null && e.clavenomina.Contains(numero))
                .Take(30)
                .ToListAsync();

            var resultados = candidatos
                .Where(e => (e.clavenomina ?? "").Trim().TrimStart('0') == numeroLimpio)
                .Select(Mapear)
                .ToList();

            return Json(resultados);
        }

        /// <summary>Buscador secundario: por nombre. Devuelve el mismo mapeo correcto.</summary>
        [HttpGet]
        public async Task<IActionResult> BuscarEmpleado(string term)
        {
            if (string.IsNullOrWhiteSpace(term) || term.Trim().Length < 2)
                return Json(Array.Empty<object>());

            term = term.Trim();

            var candidatos = await _pegaContext.VV_TARJETAS_EMPLEADOS
                .Where(x =>
                    (x.c_lname != null && x.c_lname.Contains(term)) ||
                    (x.c_mname != null && x.c_mname.Contains(term)))
                .Take(15)
                .ToListAsync();

            return Json(candidatos.Select(Mapear).ToList());
        }

        // Mapea una tarjeta de empleado al DTO que consume el buscador de la vista.
        private static object Mapear(VV_TARJETAS_EMPLEADOS e)
        {
            var clave = (e.clavenomina ?? "").Trim().TrimStart('0');
            return new
            {
                usuario = ConstruirUsuario(e),
                nombre = $"{(e.c_lname ?? "").Trim()} {(e.c_mname ?? "").Trim()}".Trim(),
                gafete = (e.ID_ICLASS ?? "").Trim(),
                numero = clave
            };
        }

        // "OTH053226" -> "OTH53226". Separa prefijo alfabético + dígitos y quita ceros.
        private static string ConstruirUsuario(VV_TARJETAS_EMPLEADOS e)
        {
            var raw = (e.c_fname ?? "").Trim();
            if (raw.Length == 0)
                raw = (e.emp ?? "").Trim() + (e.clavenomina ?? "").Trim();

            var m = Regex.Match(raw, @"^([A-Za-z]*)0*(\d+)$");
            if (m.Success)
                return (m.Groups[1].Value + m.Groups[2].Value).ToUpperInvariant();

            return raw.ToUpperInvariant();
        }
    }
}
