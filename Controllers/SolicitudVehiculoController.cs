using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AppValetParking.Data;
using AppValetParking.Models;
using ClosedXML.Excel;

namespace AppValetParking.Controllers
{
    public class SolicitudVehiculoController : Controller
    {
        private readonly ApplicationDbContext _context;

        public SolicitudVehiculoController(ApplicationDbContext context)
        {
            _context = context;
        }

        // ======================================================
        // ===============      INDEX     =======================
        // ======================================================
        public IActionResult Index(DateTime? inicio, DateTime? fin)
        {
            // ?? USAMOS LA TABLA REAL: ValetSolicitudes
            var query = _context.ValetSolicitudes.AsQueryable();

            if (inicio.HasValue)
                query = query.Where(x => x.TiempoCreado.Date >= inicio.Value.Date);

            if (fin.HasValue)
                query = query.Where(x => x.TiempoCreado.Date <= fin.Value.Date);

            var lista = query
                .OrderByDescending(x => x.TiempoCreado)
                .ToList();

            // ---------- CONTADORES ----------
            int total = lista.Count;
            int pendientes = lista.Count(x => x.TiempoAtendido == null);
            int atendidos = lista.Count(x => x.TiempoAtendido != null);

            int rapidos = lista.Count(x =>
                x.TiempoAtendido != null &&
                EF.Functions.DateDiffMinute(x.TiempoCreado, x.TiempoAtendido) <= 10
            );

            int normales = lista.Count(x =>
                x.TiempoAtendido != null &&
                EF.Functions.DateDiffMinute(x.TiempoCreado, x.TiempoAtendido) > 10 &&
                EF.Functions.DateDiffMinute(x.TiempoCreado, x.TiempoAtendido) <= 20
            );

            int lentos = lista.Count(x =>
                x.TiempoAtendido != null &&
                EF.Functions.DateDiffMinute(x.TiempoCreado, x.TiempoAtendido) > 20
            );

            ViewBag.Total = total;
            ViewBag.Pendientes = pendientes;
            ViewBag.Atendidos = atendidos;
            ViewBag.Rapidos = rapidos;
            ViewBag.Normales = normales;
            ViewBag.Lentos = lentos;

            return View(lista);
        }

        // ======================================================
        // =============== MARCAR COMO ATENDIDO =================
        // ======================================================
        [HttpPost]
        public IActionResult CambiarEstatus(int id)
        {
            var item = _context.ValetSolicitudes.Find(id);

            if (item == null)
                return NotFound();

            item.TiempoAtendido = DateTime.Now;
            _context.SaveChanges();

            return RedirectToAction("Index");
        }

        // ======================================================
        // =============== EXPORTAR EXCEL ========================
        // ======================================================
        public IActionResult ExportarExcel(DateTime? inicio, DateTime? fin)
        {
            var query = _context.ValetSolicitudes.AsQueryable();

            if (inicio.HasValue)
                query = query.Where(x => x.TiempoCreado.Date >= inicio.Value.Date);

            if (fin.HasValue)
                query = query.Where(x => x.TiempoCreado.Date <= fin.Value.Date);

            var lista = query
                .OrderByDescending(x => x.TiempoCreado)
                .ToList();

            using var workbook = new XLWorkbook();
            var ws = workbook.Worksheets.Add("Solicitudes");

            ws.Cell(1, 1).Value = "ID";
            ws.Cell(1, 2).Value = "Folio";
            ws.Cell(1, 3).Value = "Huésped";
            ws.Cell(1, 4).Value = "Habitación";
            ws.Cell(1, 5).Value = "Hotel";
            ws.Cell(1, 6).Value = "Fecha Solicitud";
            ws.Cell(1, 7).Value = "Fecha Atención";
            ws.Cell(1, 8).Value = "Minutos Atención";

            int fila = 2;

            foreach (var x in lista)
            {
                ws.Cell(fila, 1).Value = x.Id;
                ws.Cell(fila, 2).Value = x.FolioVP;
                ws.Cell(fila, 3).Value = x.NombreReserva;
                ws.Cell(fila, 4).Value = x.Habitacion;
                ws.Cell(fila, 5).Value = x.Resort;
                ws.Cell(fila, 6).Value = x.TiempoCreado;
                ws.Cell(fila, 7).Value = x.TiempoAtendido;

                if (x.TiempoAtendido != null)
                    ws.Cell(fila, 8).Value =
                        (int)(x.TiempoAtendido.Value - x.TiempoCreado).TotalMinutes;
                else
                    ws.Cell(fila, 8).Value = "PENDIENTE";

                fila++;
            }

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            stream.Position = 0;

            return File(stream.ToArray(),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                "Solicitudes.xlsx");
        }
    }
}
