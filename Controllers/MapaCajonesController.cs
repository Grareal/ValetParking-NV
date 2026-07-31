using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AppValetParking.Data;

namespace AppValetParking.Controllers
{
    public class MapaCajonesController : Controller
    {
        private readonly ApplicationDbContext _context;

        // Zonas legacy/duplicadas de la tabla Cajones que no representan una
        // zona real de asignación (espejo de cajonesZonasOcultas en
        // lib/services/valet_service.dart, para que el mapa coincida con
        // editar_screen.dart y reportes_screen.dart).
        private static readonly HashSet<string> ZonasOcultas = new() { "ANDENES", "EN.BUFFER" };

        public MapaCajonesController(ApplicationDbContext context)
        {
            _context = context;
        }

        public IActionResult Index()
        {
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> GetMapa(bool todas = false)
        {
            var cajones = await _context.Cajones.ToListAsync();
            if (!todas)
                cajones = cajones.Where(c => !ZonasOcultas.Contains(c.Ubicacion)).ToList();

            // El folio/auto se busca por el CajonBuffer guardado en ValetRegistro,
            // que puede venir en dos formatos según qué pantalla asignó el cajón:
            // "Ubicacion+Numero" (editar_screen.dart, flujo actual) o solo
            // "Numero" (BotonesView.cshtml, flujo legacy). Probamos ambos.
            var registrosActivos = await _context.ValetRegistros
                .Where(r => (r.Situacion == null || r.Situacion != "SALIDA") && !string.IsNullOrEmpty(r.CajonBuffer))
                .ToListAsync();

            var ultimoPorClave = registrosActivos
                .GroupBy(r => r.CajonBuffer!.Trim())
                .ToDictionary(g => g.Key, g => g.OrderByDescending(r => r.Fecha).First(), StringComparer.OrdinalIgnoreCase);

            var folios = ultimoPorClave.Values.Select(r => r.FolioVP).Where(f => f != null).Distinct().ToList();
            var vehiculos = await _context.VehiculosInfo
                .Where(v => folios.Contains(v.FolioVP))
                .ToListAsync();

            var resultado = cajones
                .OrderBy(c => c.Ubicacion)
                .ThenBy(c => int.TryParse(c.Numero, out var n) ? n : int.MaxValue)
                .Select(c =>
                {
                    ultimoPorClave.TryGetValue($"{c.Ubicacion}{c.Numero}", out var registro);
                    if (registro == null)
                        ultimoPorClave.TryGetValue(c.Numero, out registro);

                    var vehiculo = registro != null
                        ? vehiculos.FirstOrDefault(v => v.FolioVP == registro.FolioVP)
                        : null;

                    return new
                    {
                        c.Id,
                        c.Numero,
                        c.Ubicacion,
                        // La ocupación que se muestra es la misma que consume la app
                        // (Cajon.Ocupado vía /Cajones/GetCajones); el registro solo
                        // se usa para enriquecer con folio/auto cuando hay coincidencia.
                        Ocupado = c.Ocupado,
                        FolioVP = registro?.FolioVP,
                        NombreReserva = registro?.NombreReserva,
                        Habitacion = registro?.Habitacion,
                        Hotel = registro?.Hotel,
                        Servicio = registro?.Servicio,
                        Placas = vehiculo?.Placas,
                        Marca = vehiculo?.Marca,
                        Modelo = vehiculo?.Modelo,
                        Color = vehiculo?.Color,
                        HoraEntrada = registro?.Fecha,
                        c.UltimoMotivoLiberacion,
                        c.FechaUltimaLiberacion
                    };
                })
                .ToList();

            return Json(resultado);
        }
    }
}
