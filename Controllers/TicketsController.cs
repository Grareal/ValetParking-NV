using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Drawing.Printing;
using System.Text.Json;
using AppValetParking.Filters;
using AppValetParking.Data;
using AppValetParking.Models;
using ClosedXML.Excel;
using AppValetParking.Services;
using System.IO;

namespace AppValetParking.Controllers
{
    public class TicketsController : Controller
    {
        private readonly ValetParkingDbContext _valetContext;
        private readonly TcabdopeNewDbContext _tcabdopeContext;
        private readonly IWebHostEnvironment _env;
        private readonly ILogger<TicketsController> _logger;

        private const string RulesConfigFile = "ticket-rules.json";
        private const string PrinterConfigFile = "printer-config.json";

        public TicketsController(
    ValetParkingDbContext valetContext,
    TcabdopeNewDbContext tcabdopeContext,
    IWebHostEnvironment env,
    ILogger<TicketsController> logger)
        {
            _valetContext = valetContext;
            _tcabdopeContext = tcabdopeContext;
            _env = env;
            _logger = logger;
        }

        public IActionResult Index()
        {
            return View();
        }

        public IActionResult Historial()
        {
            return View();
        }

        // ==========================================
        // OBTENER RESERVAS DEL DÍA
        // ==========================================
        [HttpGet]
        public async Task<IActionResult> GetReservasDia()
        {
            var fechaHoy = DateTime.Today.ToString("yyyyMMdd");

            var reservas = await _tcabdopeContext.Reservas
                .Where(r =>
                    r.h_fec_lld == fechaHoy &&
                    !string.IsNullOrEmpty(r.h_res_cve) &&
                    !string.IsNullOrEmpty(r.h_cod_reserva))
                .Select(r => new
                {
                    h_status = r.h_status,
                    r.h_res_cve ,
                    r.h_nom,
                    r.h_num_hab,
                    r.Hotel,
                    r.h_tpo_hab,
                    r.h_tpo_hsp,
                    r.m_msg0,
                    r.h_cod_reserva,
                    r.h_vip
                })
                .OrderBy(r => r.Hotel)
                .ThenBy(r => r.h_num_hab)
                .ToListAsync();

            return Json(reservas);
        }

        // ==========================================
        // OBTENER RESERVAS DEL MES 
        // ==========================================


        [HttpGet]
        public async Task<IActionResult> BuscarReserva(string folio)
        {
            if (string.IsNullOrWhiteSpace(folio))
                return Json(null);

            var r = await _tcabdopeContext.ReservationSearch.Where(x => x.RESORT == "VINV" &&
                       (x.CONFIRMATION_NO == folio || x.EXTERNAL_REFERENCE == folio))
                .Select(x => new
                {
                    h_res_cve = x.CONFIRMATION_NO,
                    h_cod_reserva = x.EXTERNAL_REFERENCE,  // ← agregar esta línea
                    h_nom = (x.SGUEST_FIRSTNAME ?? "") + " " + (x.SGUEST_NAME ?? ""),
                    h_num_hab = x.ROOM,
                    Hotel = x.ROOM_CLASS_DESCRIPTION,
                    h_tpo_hsp = x.VIP ?? x.MARKET_CODE
                })
                .FirstOrDefaultAsync();

            return Json(r);
        }

        // ==========================================
        // REGLAS DE IMPRESIÓN
        // ==========================================

        [HttpGet]
        public IActionResult GetTicketRules()
        {
            var path = GetRulesPath();

            if (!System.IO.File.Exists(path))
                return Json(new List<TicketRule>());

            var json = System.IO.File.ReadAllText(path);
            var rules = JsonSerializer.Deserialize<List<TicketRule>>(json);

            return Json(rules);
        }

        [HttpPost]
        public IActionResult SaveTicketRules([FromBody] List<TicketRule> rules)
        {
            var path = GetRulesPath();
            EnsureDirectory(path);

            var json = JsonSerializer.Serialize(rules,
                new JsonSerializerOptions { WriteIndented = true });

            System.IO.File.WriteAllText(path, json);

            return Ok(new { success = true });
        }

        // ==========================================
        // IMPRESORAS INSTALADAS
        // ==========================================
        [HttpGet]
        public IActionResult GetInstalledPrinters()
        {
            var printers = PrinterSettings
                .InstalledPrinters
                .Cast<string>()
                .ToList();

            return Json(printers);
        }

        // ==========================================
        // CONFIGURACIÓN DE IMPRESORA POR HOTEL
        // ==========================================

        [HttpGet]
        public IActionResult GetPrinterConfig()
        {
            var path = GetPrinterPath();

            if (!System.IO.File.Exists(path))
                return Json(new Dictionary<string, string>());

            var json = System.IO.File.ReadAllText(path);
            var config = JsonSerializer.Deserialize<Dictionary<string, string>>(json);

            return Json(config);
        }

        [HttpPost]
        public IActionResult SavePrinterConfig([FromBody] Dictionary<string, string> config)
        {
            var path = GetPrinterPath();
            EnsureDirectory(path);

            var json = JsonSerializer.Serialize(config,
                new JsonSerializerOptions { WriteIndented = true });

            System.IO.File.WriteAllText(path, json);

            return Ok();
        }

        // ==========================================
        // VISTAS
        // ==========================================

        [HttpGet]
        public IActionResult Reservas()
        {
            return View();
        }

    
        [HttpGet]
        [Permiso("Configuracion")]  // aquí colocas el permiso que corresponde
        public IActionResult Config()
        {
            // No necesitas validar sesión ni permisos manualmente
            ViewBag.Permisos = HttpContext.Session.GetString("Permisos") ?? "";
            return View();
        }

        // ==========================================
        // IMPRIMIR TICKET
        // ==========================================

        [HttpPost]
        public async Task<IActionResult> PrintTicket([FromBody] TicketRequest request)
        {
            try
            {
                 Ticket ticket = new Ticket
                {
                    FOLIO = request.Folio,
                    NAME = request.Name,
                    ROOM = request.Room,
                    HOTEL = request.Hotel,
                    TYPE = request.Type,
                    PRINTERS = request.Printer,
                    OBS = request.Comments
                };

                ticket.Print();

                //  GUARDAR EN BD
                var ticketDb = new TicketEnviado
                {
                    Folio = request.Folio,
                    Nombre = request.Name,
                    Habitacion = request.Room,
                    Hotel = request.Hotel,
                    Tipo = request.Type,
                    Comentario = request.Comments,
                    Impresora = request.Printer,
                    FechaEnvio = DateTime.Now
                };

                _valetContext.TicketsEnviados.Add(ticketDb);
                await _valetContext.SaveChangesAsync();

                return Ok(new { success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error imprimiendo");
                return BadRequest(new
                {
                    success = false,
                    message = ex.Message
                });
            }
        }

        [HttpPost]
        public IActionResult GetTicketsEnviados()
        {
            var draw = Request.Form["draw"].FirstOrDefault();
            var start = int.TryParse(Request.Form["start"].FirstOrDefault(), out var s) ? s : 0;
            var length = int.TryParse(Request.Form["length"].FirstOrDefault(), out var l) ? l : 10;


            var busqueda = Request.Form["busqueda"].FirstOrDefault();

            var fechaInicio = Request.Form["fechaInicio"].FirstOrDefault();
            var fechaFin = Request.Form["fechaFin"].FirstOrDefault();

            var query = _valetContext.TicketsEnviados.AsQueryable();

            // 🔹 total SIN filtros
            var total = query.Count();

            // 🔹 aplicar filtros
            if (!string.IsNullOrEmpty(busqueda))
            {
                query = query.Where(x =>
                    (x.Folio ?? "").Contains(busqueda) ||
                    (x.Nombre ?? "").Contains(busqueda) ||
                    (x.Habitacion ?? "").Contains(busqueda) ||
                    (x.Comentario ?? "").Contains(busqueda)
                );
            }

            if (!string.IsNullOrEmpty(fechaInicio) && !string.IsNullOrEmpty(fechaFin) &&
                DateTime.TryParse(fechaInicio, out var fi) &&
                DateTime.TryParse(fechaFin, out var ff))
            {
                ff = ff.AddDays(1);

                query = query.Where(x => x.FechaEnvio >= fi && x.FechaEnvio < ff);
            }

            // 🔹 total filtrado
            var filtered = query.Count();

            // 🔹 paginación
            var data = query
                .OrderByDescending(x => x.FechaEnvio)
                .Skip(start)
                .Take(length)
                .ToList();

            return Json(new
            {
                draw = draw,
                recordsTotal = total,
                recordsFiltered = filtered,
                data = data
            });
        }

        [HttpGet]
        public IActionResult ExportarExcel(
      string busqueda,
      string fechaInicio,
      string fechaFin)
        {
            var query = _valetContext.TicketsEnviados.AsQueryable();

            if (!string.IsNullOrEmpty(busqueda))
            {
                query = query.Where(x =>
                    (x.Folio ?? "").Contains(busqueda) ||
                    (x.Nombre ?? "").Contains(busqueda) ||
                    (x.Habitacion ?? "").Contains(busqueda) ||
                    (x.Comentario ?? "").Contains(busqueda)
                );
            }

            if (!string.IsNullOrEmpty(fechaInicio) &&
                !string.IsNullOrEmpty(fechaFin))
            {
                DateTime fi = DateTime.Parse(fechaInicio);
                DateTime ff = DateTime.Parse(fechaFin).AddDays(1);

                query = query.Where(x =>
                    x.FechaEnvio >= fi &&
                    x.FechaEnvio < ff);
            }

            var data = query
                .OrderByDescending(x => x.FechaEnvio)
                .ToList();

            using (var workbook = new XLWorkbook())
            {
                var headers = new[] { "Reserva", "Nombre", "Habitación", "Comentario", "Hotel", "Fecha" };
                var ws = ExcelExportHelper.CreateStyledSheet(workbook, "Tickets", headers);

                int row = 2;

                foreach (var item in data)
                {
                    ws.Cell(row, 1).Value = item.Folio;
                    ws.Cell(row, 2).Value = item.Nombre;
                    ws.Cell(row, 3).Value = item.Habitacion;
                    ws.Cell(row, 4).Value = item.Comentario;
                    ws.Cell(row, 5).Value = item.Hotel;
                    ws.Cell(row, 6).Value = item.FechaEnvio;
                    ws.Cell(row, 6).Style.DateFormat.Format = "yyyy-MM-dd HH:mm";

                    row++;
                }

                ExcelExportHelper.FinalizeStyledSheet(ws, row - 1, headers.Length);

                using (var stream = new MemoryStream())
                {
                    workbook.SaveAs(stream);

                    var content = stream.ToArray();

                    return File(
                        content,
                        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                        $"Tickets_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx");
                }
            }
        }

        // ==========================================
        // MÉTODOS PRIVADOS
        // ==========================================

        [HttpGet]
        public async Task<IActionResult> GetTicketsEnviadosHoy()
        {
            var hoy = DateTime.Today;

            var ticketsHoy = await _valetContext.TicketsEnviados
                .Where(t => t.FechaEnvio >= hoy && t.FechaEnvio < hoy.AddDays(1))
                .Select(t => t.Folio)
                .ToListAsync();

            return Json(ticketsHoy);
        }

        private string GetRulesPath()
        {
            return Path.Combine(_env.ContentRootPath, "Data", RulesConfigFile);
        }

        private string GetPrinterPath()
        {
            return Path.Combine(_env.ContentRootPath, "Data", PrinterConfigFile);
        }

        private void EnsureDirectory(string path)
        {
            var dir = Path.GetDirectoryName(path);
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);
        }
    }
}