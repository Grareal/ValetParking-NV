using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AppValetParking.Data;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace AppValetParking.Controllers
{
    [ApiController]
    [Route("api/dashboard")]
    public class DashboardApiController : Controller
    {
        private readonly ApplicationDbContext _context;

        public DashboardApiController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet("resumen")]
        public async Task<IActionResult> Resumen()
        {
            var hoy = DateTime.Today;
            var maniana = hoy.AddDays(1);

            int vehiculosActivos = await _context.VehiculosInfo
                .CountAsync(v => v.Estatus != "Fuera");

            int entradasHoy = await _context.ValetRegistros
                .CountAsync(r => r.Fecha >= hoy && r.Fecha < maniana);

            int salidasPermanentesHoy = await _context.ValetMovimientos
                .CountAsync(m => m.Servicio == "SALIDA" && m.FechaHora >= hoy && m.FechaHora < maniana);

            // Salidas parciales activas: coches en paseo que se espera regresen.
            int salidasParcialesActivas = await _context.VehiculosInfo
                .CountAsync(v => v.Estatus == "Parcial");

            var solicitudesAtendidasHoy = await _context.ValetSolicitudes
                .Where(s => s.TiempoAtendido != null && s.TiempoCreado >= hoy && s.TiempoCreado < maniana)
                .Select(s => new { s.TiempoCreado, s.TiempoAtendido })
                .ToListAsync();

            double tiempoPromedioMinutos = solicitudesAtendidasHoy.Count > 0
                ? solicitudesAtendidasHoy.Average(s => (s.TiempoAtendido!.Value - s.TiempoCreado).TotalMinutes)
                : 0;

            return Ok(new
            {
                vehiculosActivos,
                entradasHoy,
                salidasPermanentesHoy,
                salidasParcialesActivas,
                tiempoPromedioMinutos = Math.Round(tiempoPromedioMinutos, 1)
            });
        }

        [HttpGet("actividad")]
        public async Task<IActionResult> Actividad(int take = 10)
        {
            var movimientos = await _context.ValetMovimientos
                .Include(m => m.ValetRegistro)
                .OrderByDescending(m => m.FechaHora)
                .Take(take)
                .Select(m => new
                {
                    tipo = "movimiento",
                    fecha = m.FechaHora ?? DateTime.MinValue,
                    folio = m.ValetRegistro != null ? m.ValetRegistro.FolioVP : null,
                    detalle = m.MovimientoTexto
                })
                .ToListAsync();

            var solicitudes = await _context.ValetSolicitudes
                .OrderByDescending(s => s.FechaSolicitud)
                .Take(take)
                .Select(s => new
                {
                    tipo = "solicitud",
                    fecha = s.FechaSolicitud,
                    folio = s.FolioVP,
                    detalle = s.TiempoAtendido != null
                        ? $"Solicitud atendida para {s.NombreReserva} (hab. {s.Habitacion})"
                        : $"Solicitud pendiente para {s.NombreReserva} (hab. {s.Habitacion})"
                })
                .ToListAsync();

            var combinado = movimientos
                .Concat(solicitudes)
                .OrderByDescending(x => x.fecha)
                .Take(take)
                .ToList();

            return Ok(combinado);
        }
    }
}
