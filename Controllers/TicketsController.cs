using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Drawing.Printing;
using System.Text.Json;
using AppValetParking.Data;
using AppValetParking.Models;
using AppValetParking.Services;

namespace AppValetParking.Controllers
{
    public class TicketsController : Controller
    {
        private readonly TcabdopeNewDbContext _context;
        private readonly IWebHostEnvironment _env;
        private readonly ILogger<TicketsController> _logger;

        private const string PrinterConfigFile = "printer-config.json";

        public TicketsController(
            TcabdopeNewDbContext context,
            IWebHostEnvironment env,
            ILogger<TicketsController> logger)
        {
            _context = context;
            _env = env;
            _logger = logger;
        }

        public IActionResult Index()
        {
            return View();
        }

        // ==========================================
        // OBTENER RESERVAS DEL DÍA (SOLO DB REAL)
        // ==========================================
        [HttpGet]
        public async Task<IActionResult> GetReservasDia()
        {
            var fechaHoy = DateTime.Today.ToString("yyyyMMdd");

            var reservas = await _context.Reservas
                .Where(r => r.h_fec_lld == fechaHoy)
                .Select(r => new
                {
                    r.h_res_cve,
                    r.h_nom,
                    r.h_num_hab,
                    r.Hotel,          // <-- aquí está la corrección
                    r.h_tpo_hab,      // <-- aquí está la corrección
                    r.h_tpo_hsp,
                    r.m_msg0,
                    r.h_cod_reserva // <-- aquí debe llamarse así

                })
.OrderBy(r => r.Hotel)

                .ThenBy(r => r.h_num_hab)
                .ToListAsync();

            return Json(reservas);
        }

        // ==========================================
        // IMPRESORAS INSTALADAS EN EL SERVIDOR
        // ==========================================
        [HttpGet]
        public IActionResult GetInstalledPrinters()
        {
            var printers = PrinterSettings.InstalledPrinters.Cast<string>().ToList();
            return Json(printers);
        }

        // ==========================================
        // CONFIGURACIÓN IMPRESORA POR HOTEL
        // ==========================================
        [HttpGet]
        public IActionResult GetPrinterConfig()
        {
            var path = Path.Combine(_env.ContentRootPath, "Data", PrinterConfigFile);

            if (!System.IO.File.Exists(path))
                return Json(new Dictionary<string, string>());

            var json = System.IO.File.ReadAllText(path);
            var config = JsonSerializer.Deserialize<Dictionary<string, string>>(json);

            return Json(config);
        }

        [HttpPost]
        public IActionResult SavePrinterConfig([FromBody] Dictionary<string, string> config)
        {
            var path = Path.Combine(_env.ContentRootPath, "Data", PrinterConfigFile);

            var dir = Path.GetDirectoryName(path);
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            var json = JsonSerializer.Serialize(config, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            System.IO.File.WriteAllText(path, json);

            return Ok();
        }

        [HttpGet]
        public IActionResult Reservas()
        {
            return View();
        }


        // ==========================================
        // IMPRIMIR RESERVA
        // ==========================================

        [HttpPost]
        public IActionResult PrintTicket([FromBody] TicketRequest request)
        {
            try
            {
                Ticket ticket = new Ticket
                {
                    FOLIO = request.Folio,
                    NAME = request.Name,
                    ROOM = request.Room,
                    HOTEL = request.Hotel,
                    PRINTERS = request.Printer,
                    OBS = request.Comments   //  
                };

                ticket.Print();

                return Ok(new { success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error imprimiendo");
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

    }
}