using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AppValetParking.Data;
using AppValetParking.Models;
using ClosedXML.Excel;
using AppValetParking.Services;

namespace AppValetParking.Controllers
{
    public class ReportesController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ReportesController(ApplicationDbContext context)
        {
            _context = context;
        }

        // =============================== INDEX ===============================
        public IActionResult Index()
        {
            ViewBag.Servicios = _context.ValetRegistros
                .Where(v => v.Servicio != null)
                .Select(v => v.Servicio!)
                .Distinct()
                .OrderBy(s => s)
                .ToList();

            ViewBag.Valets = _context.ValetRegistros
                .Where(v => v.Valet != null)
                .Select(v => v.Valet!)
                .Distinct()
                .OrderBy(s => s)
                .ToList();

            ViewBag.Hoteles = _context.ValetRegistros
                .Where(v => v.Hotel != null)
                .Select(v => v.Hotel!)
                .Distinct()
                .OrderBy(s => s)
                .ToList();

            return View();
        }

        // ====================================================================
        //  DATATABLE
        // ====================================================================
        [HttpPost]
        public async Task<IActionResult> GetData(DateTime? inicio, DateTime? fin, string? servicio, string? valet, string? hotel)
        {
            var form = Request.Form;
            int start = Convert.ToInt32(form["start"]);
            int length = Convert.ToInt32(form["length"]);
            string searchValue = form["search[value]"];
            string draw = form["draw"];

            var q = _context.ValetRegistros.AsQueryable();

            // FILTROS
            if (inicio.HasValue)
                q = q.Where(x => x.Fecha.Date >= inicio.Value);

            if (fin.HasValue)
                q = q.Where(x => x.Fecha.Date <= fin.Value);

            if (!string.IsNullOrWhiteSpace(servicio))
                q = q.Where(x => x.Servicio == servicio);

            if (!string.IsNullOrWhiteSpace(valet))
                q = q.Where(x => x.Valet == valet);

            if (!string.IsNullOrWhiteSpace(hotel))
                q = q.Where(x => x.Hotel == hotel);

            // BUSQUEDA GENERAL
            if (!string.IsNullOrWhiteSpace(searchValue))
            {
                q = q.Where(x =>
                    (x.Habitacion ?? "").Contains(searchValue) ||
                    (x.Hotel ?? "").Contains(searchValue) ||
                    (x.Servicio ?? "").Contains(searchValue) ||
                    (x.Estatus ?? "").Contains(searchValue) ||
                    (x.Valet ?? "").Contains(searchValue)
                );
            }

            int total = await q.CountAsync();
            var data = await q
                .OrderByDescending(x => x.Fecha)
                .Skip(start)
                .Take(length)
                .ToListAsync();

            return Json(new
            {
                draw,
                recordsTotal = total,
                recordsFiltered = total,
                data
            });
        }

        // ====================================================================
        //  CARS PER DAY
        // ====================================================================
        public IActionResult CarsPerDay(DateTime? inicio, DateTime? fin, string? servicio, string? valet, string? hotel)
        {
            var query = _context.ValetRegistros.AsQueryable();

            if (inicio.HasValue)
                query = query.Where(x => x.Fecha.Date >= inicio.Value);

            if (fin.HasValue)
                query = query.Where(x => x.Fecha.Date <= fin.Value);

            if (!string.IsNullOrWhiteSpace(servicio))
                query = query.Where(x => x.Servicio == servicio);

            if (!string.IsNullOrWhiteSpace(valet))
                query = query.Where(x => x.Valet == valet);

            if (!string.IsNullOrWhiteSpace(hotel))
                query = query.Where(x => x.Hotel == hotel);

            var result = query
                .GroupBy(x => x.Fecha.Date)
                .Select(g => new {
                    date = g.Key.ToString("yyyy-MM-dd"),
                    count = g.Count()
                })
                .ToList();

            return Json(result);
        }

        // ====================================================================
        //  MOVIMIENTOS POR VALET
        // ====================================================================
        [HttpGet]
        public async Task<IActionResult> MovimientosPorValet(DateTime? inicio, DateTime? fin, int top = 10)
        {
            var q = _context.ValetRegistros.AsQueryable();

            if (inicio.HasValue)
                q = q.Where(x => x.Fecha.Date >= inicio.Value);
            if (fin.HasValue)
                q = q.Where(x => x.Fecha.Date <= fin.Value);

            var data = await q
                .Where(x => x.Valet != null)
                .GroupBy(x => x.Valet!)
                .Select(g => new { valet = g.Key, count = g.Count() })
                .OrderByDescending(g => g.count)
                .Take(top)
                .ToListAsync();

            return Json(data);
        }

        // ====================================================================
        //  DETALLE DE VEHICULO + FOTOS (por folio)
        // ====================================================================
        [HttpGet]
        public async Task<IActionResult> GetDetalleFolio(string folio)
        {
            if (string.IsNullOrWhiteSpace(folio))
                return BadRequest(new { mensaje = "Folio requerido" });

            var registro = await _context.ValetRegistros
                .FirstOrDefaultAsync(x => x.FolioVP == folio);

            var vehiculo = await _context.VehiculosInfo
                .FirstOrDefaultAsync(x => x.FolioVP == folio);

            var fotos = await _context.VehiculoFotos
                .Where(f => f.FolioVP == folio)
                .Select(f => new { f.Slot, f.RutaArchivo })
                .ToListAsync();

            return Json(new
            {
                folio,
                hotel = registro?.Hotel,
                habitacion = registro?.Habitacion,
                nombreReserva = registro?.NombreReserva,
                servicio = registro?.Servicio,
                valet = registro?.Valet,
                placas = vehiculo?.Placas,
                marca = vehiculo?.Marca,
                modelo = vehiculo?.Modelo,
                color = vehiculo?.Color,
                estatus = vehiculo?.Estatus,
                fotos
            });
        }

        // ====================================================================
        //  EXPORTAR EXCEL
        // ====================================================================
        [HttpPost]
        public async Task<IActionResult> ExportarExcel(DateTime? inicio, DateTime? fin, string? servicio, string? valet, string? hotel)
        {
            var q = _context.ValetRegistros.AsQueryable();

            if (inicio.HasValue)
                q = q.Where(x => x.Fecha.Date >= inicio.Value);

            if (fin.HasValue)
                q = q.Where(x => x.Fecha.Date <= fin.Value);

            if (!string.IsNullOrWhiteSpace(servicio))
                q = q.Where(x => x.Servicio == servicio);

            if (!string.IsNullOrWhiteSpace(valet))
                q = q.Where(x => x.Valet == valet);

            if (!string.IsNullOrWhiteSpace(hotel))
                q = q.Where(x => x.Hotel == hotel);

            var list = await q.OrderBy(x => x.Fecha).ToListAsync();

            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add("Reporte");

            var table = ws.Cell(1, 1).InsertTable(list);
            ExcelExportHelper.StyleInsertedTable(table);

            using var ms = new MemoryStream();
            wb.SaveAs(ms);

            return File(ms.ToArray(),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"ReporteValet_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx");
        }
    }
}
