using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AppValetParking.Data;
using AppValetParking.Models;
using ClosedXML.Excel;

namespace AppValetParking.Controllers
{
    public class ControlSolicitudesController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ControlSolicitudesController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index(DateTime? inicio, DateTime? fin)
        {
             inicio ??= DateTime.Today;
            fin ??= DateTime.Today.AddDays(1).AddSeconds(-1);

            var solicitudes = await _context.ValetSolicitudes
                .Where(s => s.FechaSolicitud >= inicio && s.FechaSolicitud <= fin)
                .OrderByDescending(s => s.FechaSolicitud)
                .ToListAsync();

            // CONTADORES
            int total = solicitudes.Count;
            int pendientes = solicitudes.Count(s => s.TiempoAtendido == null);
            int atendidos = solicitudes.Count(s => s.TiempoAtendido != null);

            var tiempos = solicitudes
                .Where(s => s.TiempoAtendido != null)
                .Select(s => (s.TiempoAtendido.Value - s.TiempoCreado).TotalMinutes)
                .ToList();

            int rapidos = tiempos.Count(t => t <= 10);
            int normales = tiempos.Count(t => t > 10 && t <= 20);
            int lentos = tiempos.Count(t => t > 20);

            ViewBag.Total = total;
            ViewBag.Pendientes = pendientes;
            ViewBag.Atendidos = atendidos;
            ViewBag.Rapidos = rapidos;
            ViewBag.Normales = normales;
            ViewBag.Lentos = lentos;

            ViewBag.Inicio = inicio?.ToString("yyyy-MM-dd");
            ViewBag.Fin = fin?.ToString("yyyy-MM-dd");

            return View(solicitudes);
        }


        // =================== CAMBIAR ESTATUS ===================
        [HttpPost]
        public async Task<IActionResult> CambiarEstatus(int id)
        {
            var registro = await _context.ValetSolicitudes.FindAsync(id);
            if (registro != null && registro.TiempoAtendido == null)
            {
                registro.TiempoAtendido = DateTime.Now;
                await _context.SaveChangesAsync();
            }
            return RedirectToAction("Index");
        }

        // ===================== EXPORTAR EXCEL ======================
        public IActionResult Excel(DateTime? inicio, DateTime? fin)
        {
            inicio ??= DateTime.Today;
            fin ??= DateTime.Today.AddDays(1).AddSeconds(-1);

            var data = _context.ValetSolicitudes
                .Where(s => s.FechaSolicitud >= inicio && s.FechaSolicitud <= fin)
                .OrderByDescending(s => s.FechaSolicitud)
                .ToList();

            using var wb = new XLWorkbook();
            var ws = wb.AddWorksheet("Solicitudes");

            ws.Cell(1, 1).Value = "Folio";
            ws.Cell(1, 2).Value = "Huésped";
            ws.Cell(1, 3).Value = "Habitación";
            ws.Cell(1, 4).Value = "Hotel";
            ws.Cell(1, 5).Value = "Fecha Solicitud";
            ws.Cell(1, 6).Value = "Atendido";
            ws.Cell(1, 7).Value = "Tiempo (minutos)";

            int row = 2;

            foreach (var s in data)
            {
                ws.Cell(row, 1).Value = s.FolioVP;
                ws.Cell(row, 2).Value = s.NombreReserva;
                ws.Cell(row, 3).Value = s.Habitacion;
                ws.Cell(row, 4).Value = s.Resort;
                ws.Cell(row, 5).Value = s.FechaSolicitud;

                if (s.TiempoAtendido != null)
                {
                    ws.Cell(row, 6).Value = s.TiempoAtendido.Value;
                    ws.Cell(row, 7).Value = (s.TiempoAtendido.Value - s.TiempoCreado).TotalMinutes;
                }
                else
                {
                    ws.Cell(row, 6).Value = "Pendiente";
                }

                row++;
            }

            using var stream = new MemoryStream();
            wb.SaveAs(stream);

            return File(stream.ToArray(),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"ReporteSolicitudes_{DateTime.Today:yyyyMMdd}.xlsx");
        }

    }
}
