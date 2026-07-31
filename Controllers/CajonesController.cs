using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AppValetParking.Data;
using AppValetParking.Models;

namespace AppValetParking.Controllers
{
    public class CajonesController : Controller
    {
        private readonly ApplicationDbContext _context;
        public CajonesController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> GetCajones()
        {
            var cajones = await _context.Cajones.ToListAsync();
            var ordenados = cajones
                .OrderBy(c => c.Ubicacion)
                .ThenBy(c => int.TryParse(c.Numero, out var n) ? n : int.MaxValue)
                .ToList();
            return Json(ordenados);
        }

        // ── Administración de cajones (desde el Mapa) ────────────
        [HttpPost]
        public async Task<IActionResult> CrearCajon(string ubicacion, string numero)
        {
            ubicacion = (ubicacion ?? "").Trim().ToUpperInvariant();
            numero = (numero ?? "").Trim();
            if (string.IsNullOrEmpty(ubicacion) || string.IsNullOrEmpty(numero))
                return Ok(new { success = false, mensaje = "Zona y número son obligatorios." });

            var existe = await _context.Cajones.AnyAsync(c => c.Ubicacion == ubicacion && c.Numero == numero);
            if (existe)
                return Ok(new { success = false, mensaje = $"El cajón {ubicacion}{numero} ya existe." });

            _context.Cajones.Add(new Cajon { Ubicacion = ubicacion, Numero = numero, Ocupado = false });
            await _context.SaveChangesAsync();
            return Ok(new { success = true });
        }

        // Crea de un jalón un rango de cajones en una zona (ej. A del 1 al 20),
        // omitiendo los que ya existan.
        [HttpPost]
        public async Task<IActionResult> CrearRango(string ubicacion, int desde, int hasta)
        {
            ubicacion = (ubicacion ?? "").Trim().ToUpperInvariant();
            if (string.IsNullOrEmpty(ubicacion))
                return Ok(new { success = false, mensaje = "La zona es obligatoria." });
            if (hasta < desde || (hasta - desde) > 500)
                return Ok(new { success = false, mensaje = "Rango inválido (máx. 500 cajones)." });

            var existentes = await _context.Cajones
                .Where(c => c.Ubicacion == ubicacion)
                .Select(c => c.Numero)
                .ToListAsync();
            var set = new HashSet<string>(existentes);

            int creados = 0;
            for (int n = desde; n <= hasta; n++)
            {
                var num = n.ToString();
                if (set.Contains(num)) continue;
                _context.Cajones.Add(new Cajon { Ubicacion = ubicacion, Numero = num, Ocupado = false });
                creados++;
            }
            await _context.SaveChangesAsync();
            return Ok(new { success = true, creados });
        }

        // Borra un cajón. No permite borrar uno ocupado (hay que liberarlo antes).
        [HttpPost]
        public async Task<IActionResult> EliminarCajon(int id)
        {
            var c = await _context.Cajones.FindAsync(id);
            if (c == null) return Ok(new { success = false, mensaje = "Cajón no encontrado." });
            if (c.Ocupado)
                return Ok(new { success = false, mensaje = "No se puede borrar un cajón ocupado. Libéralo primero." });
            _context.Cajones.Remove(c);
            await _context.SaveChangesAsync();
            return Ok(new { success = true });
        }

        [HttpPost]
        public async Task<IActionResult> OcupaCajon(string numero, string ubicacion)
        {
            var cajon = await _context.Cajones
                .FirstOrDefaultAsync(c => c.Numero == numero && c.Ubicacion == ubicacion);
            if (cajon != null)
            {
                cajon.Ocupado = true;
                // Un nuevo vehículo ocupa el cajón: el señalamiento de la
                // liberación anterior ya no aplica.
                cajon.UltimoMotivoLiberacion = null;
                cajon.FechaUltimaLiberacion = null;
                await _context.SaveChangesAsync();
                return Ok();
            }
            return NotFound();
        }

        [HttpPost]
        public async Task<IActionResult> LiberarCajon(string numero, string ubicacion)
        {
            var cajon = await _context.Cajones
                .FirstOrDefaultAsync(c => c.Numero == numero && c.Ubicacion == ubicacion);

            if (cajon != null)
            {
                cajon.Ocupado = false;
                await _context.SaveChangesAsync();
                return Ok();
            }

            return NotFound();
        }

        /// Libera un cajón forzosamente por error/falla, registrando el motivo
        /// con asterisco (*) en ValetSolicitudes para trazabilidad.
        [HttpPost]
        public async Task<IActionResult> LiberarForzado(string numero, string ubicacion, string motivo)
        {
            var cajon = await _context.Cajones
                .FirstOrDefaultAsync(c => c.Numero == numero && c.Ubicacion == ubicacion);

            if (cajon == null)
                return NotFound(new { success = false, mensaje = "Cajón no encontrado." });

            cajon.Ocupado = false;
            cajon.UltimoMotivoLiberacion = string.IsNullOrWhiteSpace(motivo)
                ? "Sin motivo especificado"
                : motivo.TrimStart('*', ' ');
            cajon.FechaUltimaLiberacion = DateTime.Now;
            await _context.SaveChangesAsync();

            // Guardar log con asterisco en solicitudes para trazabilidad.
            try
            {
                var log = new ValetSolicitud
                {
                    FolioVP        = $"FORZADO-{ubicacion}{numero}",
                    NombreReserva  = "SISTEMA",
                    Resort         = "VALET",
                    TipoSalida     = "LIBERACION_FORZADA",
                    Comentarios    = string.IsNullOrWhiteSpace(motivo)
                                         ? "* Sin motivo especificado"
                                         : (motivo.StartsWith("*") ? motivo : $"* {motivo}"),
                    FechaSolicitud = DateTime.Now,
                    TiempoCreado   = DateTime.Now,
                };
                _context.ValetSolicitudes.Add(log);
                await _context.SaveChangesAsync();
            }
            catch
            {
                // El log es secundario; la liberación ya ocurrió.
            }

            return Ok(new
            {
                success = true,
                mensaje = $"Cajón {ubicacion}{numero} liberado forzosamente. Motivo registrado."
            });
        }
    }
}
