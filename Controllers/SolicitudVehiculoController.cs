using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AppValetParking.Data;
using AppValetParking.Models;
using ClosedXML.Excel;
using AppValetParking.Services;

namespace AppValetParking.Controllers
{
    [ApiController]
    [Route("api/[controller]")] 
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
            var query = _context.ValetSolicitudes.AsQueryable();

            if (inicio.HasValue)
                query = query.Where(x => x.TiempoCreado.Date >= inicio.Value.Date);

            if (fin.HasValue)
                query = query.Where(x => x.TiempoCreado.Date <= fin.Value.Date);

            var lista = query
                .OrderByDescending(x => x.TiempoCreado)
                .AsNoTracking()
                .ToList();

            // ---------- CONTADORES ----------
            int total = lista.Count;
            int pendientes = lista.Count(x => x.TiempoAtendido == null);
            int atendidos = lista.Count(x => x.TiempoAtendido != null);

            int rapidos = lista.Count(x =>
                x.TiempoAtendido != null &&
                (x.TiempoAtendido.Value - x.TiempoCreado).TotalMinutes <= 10
            );

            int normales = lista.Count(x =>
                x.TiempoAtendido != null &&
                (x.TiempoAtendido.Value - x.TiempoCreado).TotalMinutes > 10 &&
                (x.TiempoAtendido.Value - x.TiempoCreado).TotalMinutes <= 20
            );

            int lentos = lista.Count(x =>
                x.TiempoAtendido != null &&
                (x.TiempoAtendido.Value - x.TiempoCreado).TotalMinutes > 20
            );

            ViewBag.Total = total;
            ViewBag.Pendientes = pendientes;
            ViewBag.Atendidos = atendidos;
            ViewBag.Rapidos = rapidos;
            ViewBag.Normales = normales;
            ViewBag.Lentos = lentos;

            return View(lista);
        }

        [HttpPost("crear")]
        public async Task<IActionResult> CrearSolicitud([FromBody] ValetSolicitud solicitud)
        {
            var solicitudExistente = await _context.ValetSolicitudes
                .Where(x => x.FolioVP == solicitud.FolioVP &&
                            x.TiempoAtendido == null)
                .FirstOrDefaultAsync();

            if (solicitudExistente != null)
            {
                return Ok(new
                {
                    exito = false,
                    mensaje = "Este veh�culo ya fue solicitado y est� en proceso."
                });
            }

            solicitud.FechaSolicitud = DateTime.Now;

            _context.ValetSolicitudes.Add(solicitud);

            // Buscar veh�culo asociado
            var vehiculo = await _context.VehiculosInfo
                .FirstOrDefaultAsync(x => x.FolioVP == solicitud.FolioVP);

            if (vehiculo != null && !string.IsNullOrEmpty(solicitud.TipoSalida))
            {
                var tipo = solicitud.TipoSalida.Trim().ToUpperInvariant();

                if (tipo == "PARCIAL" || tipo == "PASEO")
                    vehiculo.Estatus = "Parcial";
                else if (tipo == "PERMANENTE" || tipo == "SALIDA")
                    vehiculo.Estatus = "Fuera";
                else if (tipo == "REGRESO")
                    vehiculo.Estatus = "Dentro";
            }

            await _context.SaveChangesAsync();

            return Ok(new
            {
                exito = true,
                mensaje = "Solicitud registrada correctamente."
            });
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
            var headers = new[]
            {
                "ID", "Folio", "Huésped", "Habitación", "Hotel",
                "Fecha Solicitud", "Fecha Atención", "Minutos Atención"
            };
            var ws = ExcelExportHelper.CreateStyledSheet(workbook, "Solicitudes", headers);

            int fila = 2;

            foreach (var x in lista)
            {
                ws.Cell(fila, 1).Value = x.Id;
                ws.Cell(fila, 2).Value = x.FolioVP;
                ws.Cell(fila, 3).Value = x.NombreReserva;
                ws.Cell(fila, 4).Value = x.Habitacion;
                ws.Cell(fila, 5).Value = x.Resort;
                ws.Cell(fila, 6).Value = x.TiempoCreado;
                ws.Cell(fila, 6).Style.DateFormat.Format = "yyyy-MM-dd HH:mm";
                ws.Cell(fila, 7).Value = x.TiempoAtendido;
                ws.Cell(fila, 7).Style.DateFormat.Format = "yyyy-MM-dd HH:mm";

                if (x.TiempoAtendido != null)
                    ws.Cell(fila, 8).Value =
                        (int)(x.TiempoAtendido.Value - x.TiempoCreado).TotalMinutes;
                else
                    ws.Cell(fila, 8).Value = "PENDIENTE";

                fila++;
            }

            ExcelExportHelper.FinalizeStyledSheet(ws, fila - 1, headers.Length);

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            stream.Position = 0;

            return File(stream.ToArray(),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"Solicitudes_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx");
        }
    }
}
