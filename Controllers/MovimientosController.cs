using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AppValetParking.Data;

namespace AppValetParking.Controllers
{
    /// Vista de solo lectura sobre ValetMovimientos (el log central que ya
    /// alimentan TomarSolicitud/EntregarSolicitud en ControlSolicitudesController),
    /// para tener una sola pantalla con todo el historial de movimientos sin
    /// tocar el resto de los flujos existentes.
    public class MovimientosController : Controller
    {
        private readonly ApplicationDbContext _context;

        public MovimientosController(ApplicationDbContext context)
        {
            _context = context;
        }

        public IActionResult Index()
        {
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> GetMovimientos(
            DateTime? inicio, DateTime? fin, string? folio, string? operador)
        {
            var desde = (inicio ?? DateTime.Today.AddDays(-1)).Date;
            // "fin" llega como fecha (sin hora) desde el <input type="date">;
            // se extiende al final del día para no perder movimientos de esa fecha.
            var hasta = (fin ?? DateTime.Today).Date.AddDays(1).AddSeconds(-1);

            var query =
                from m in _context.ValetMovimientos
                join r in _context.ValetRegistros on m.IdRegistro equals r.Id into regs
                from r in regs.DefaultIfEmpty()
                where m.FechaHora >= desde && m.FechaHora <= hasta
                select new
                {
                    m.Id,
                    m.FechaHora,
                    m.Reserva,
                    m.Servicio,
                    m.Operador,
                    m.MovimientoTexto,
                    FolioVP = r != null ? r.FolioVP : null,
                    NombreReserva = r != null ? r.NombreReserva : null,
                    Habitacion = r != null ? r.Habitacion : null,
                    Hotel = r != null ? r.Hotel : null,
                };

            if (!string.IsNullOrWhiteSpace(folio))
                query = query.Where(x => x.FolioVP != null && x.FolioVP.Contains(folio));

            if (!string.IsNullOrWhiteSpace(operador))
                query = query.Where(x => x.Operador != null && x.Operador.Contains(operador));

            var lista = await query
                .OrderByDescending(x => x.FechaHora)
                .Take(500)
                .ToListAsync();

            return Ok(lista);
        }
    }
}
