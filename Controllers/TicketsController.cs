using Microsoft.AspNetCore.Mvc;
using AppValetParking.Data;
using AppValetParking.Models;
using AppValetParking.Services;
using Microsoft.EntityFrameworkCore;
using System.Drawing.Printing;
using System.Text.Json;

namespace AppValetParking.Controllers
{
    public class TicketsController : Controller
    {
        private readonly TcabdopeNewDbContext _context;
        private readonly PrinterConfigService _configService;
        private readonly IWebHostEnvironment _env;
        private readonly ILogger<TicketsController> _logger;
        private const string HotelConfigFileName = "hotel-printer-config.json";

        public TicketsController(
            TcabdopeNewDbContext context,
            PrinterConfigService configService,
            IWebHostEnvironment env,
            ILogger<TicketsController> logger)
        {
            _context = context;
            _configService = configService;
            _env = env;
            _logger = logger;
        }

        public IActionResult Config()
        {
            _logger.LogInformation("Vista Config de Tickets abierta");
            return View();
        }

        public IActionResult Reservas()
        {
            _logger.LogInformation("Vista Reservas de Tickets abierta");
            return View();
        }

        public IActionResult Index()
        {
            _logger.LogInformation("Se abrió la vista Index de Tickets");
            return View();
        }

        [HttpGet]
        public IActionResult GetInstalledPrinters()
        {
            try
            {
                var list = PrinterSettings.InstalledPrinters.Cast<string>().ToList();
                _logger.LogInformation("Se obtuvieron {Count} impresoras instaladas", list.Count);
                return Json(list);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener impresoras instaladas");
                return StatusCode(500, "Error interno");
            }
        }

        [HttpGet]
        public IActionResult GetPrinterConfigs()
        {
            try
            {
                var configs = _configService.GetAll();
                _logger.LogInformation("Se cargaron {Count} configuraciones de impresora", configs.Count);
                return Json(configs);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener configuraciones de impresora");
                return StatusCode(500, "Error interno");
            }
        }

        [HttpPost]
        public IActionResult SavePrinterConfig([FromBody] PrinterConfig cfg)
        {
            try
            {
                _configService.AddOrUpdate(cfg);
                _logger.LogInformation("Se guardó configuración: {Hostname} -> {Printers}", cfg.Hostname, cfg.Printers);
                return Ok(new { success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al guardar configuración de impresora");
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetReservasDia()
        {
            try
            {
                var fechaHoy = DateTime.Today.ToString("yyyyMMdd");
                var reservas = await _context.Reservas
                    .Where(r => r.h_fec_lld == fechaHoy)
                    .OrderBy(r => r.h_num_hab)
                    .ToListAsync();

                _logger.LogInformation("Se cargaron {Count} reservas del día {Fecha}", reservas.Count, fechaHoy);
                return Json(reservas);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener reservas del día");
                return StatusCode(500, "Error interno");
            }
        }

        [HttpGet]
        public IActionResult GetHotelPrinterConfigs()
        {
            try
            {
                var configPath = Path.Combine(_env.ContentRootPath, "Data", HotelConfigFileName);

                if (!System.IO.File.Exists(configPath))
                {
                    // Crear configuración por defecto si no existe
                    var defaultConfig = new Dictionary<string, HotelPrinterConfig>
                    {
                        ["NULL"] = new HotelPrinterConfig { Printer = "", UseGeneral = true },
                        ["Beauty of Nature"] = new HotelPrinterConfig { Printer = "", UseGeneral = true },
                        ["Celebrate Park"] = new HotelPrinterConfig { Printer = "", UseGeneral = true },
                        ["Kingdom Of The Sun"] = new HotelPrinterConfig { Printer = "", UseGeneral = true },
                        ["Mayan Palace"] = new HotelPrinterConfig { Printer = "", UseGeneral = true },
                        ["Sea Garden"] = new HotelPrinterConfig { Printer = "", UseGeneral = true },
                        ["The Estates"] = new HotelPrinterConfig { Printer = "", UseGeneral = true },
                        ["The Grand Bliss"] = new HotelPrinterConfig { Printer = "", UseGeneral = true },
                        ["The Grand Luxxe"] = new HotelPrinterConfig { Printer = "", UseGeneral = true },
                        ["The Grand Mayan"] = new HotelPrinterConfig { Printer = "", UseGeneral = true }
                    };

                    // Asegurarse de que el directorio existe
                    var dataDir = Path.GetDirectoryName(configPath);
                    if (!Directory.Exists(dataDir))
                    {
                        Directory.CreateDirectory(dataDir);
                    }

                    System.IO.File.WriteAllText(configPath, JsonSerializer.Serialize(defaultConfig, new JsonSerializerOptions { WriteIndented = true }));

                    _logger.LogInformation("Configuración por defecto creada para hoteles");
                    return Json(defaultConfig);
                }

                var json = System.IO.File.ReadAllText(configPath);
                var config = JsonSerializer.Deserialize<Dictionary<string, HotelPrinterConfig>>(json);

                _logger.LogInformation("Se cargaron {Count} configuraciones de impresora por hotel", config.Count);
                return Json(config);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener configuraciones de impresora por hotel");
                return StatusCode(500, "Error interno");
            }
        }

        [HttpPost]
        public IActionResult SaveHotelPrinterConfigs([FromBody] Dictionary<string, HotelPrinterConfig> configs)
        {
            try
            {
                var configPath = Path.Combine(_env.ContentRootPath, "Data", HotelConfigFileName);

                // Asegurarse de que el directorio existe
                var dataDir = Path.GetDirectoryName(configPath);
                if (!Directory.Exists(dataDir))
                {
                    Directory.CreateDirectory(dataDir);
                }

                System.IO.File.WriteAllText(configPath, JsonSerializer.Serialize(configs, new JsonSerializerOptions { WriteIndented = true }));

                _logger.LogInformation("Configuración de impresoras por hotel guardada correctamente");
                return Ok(new { success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al guardar configuración de impresoras por hotel");
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        public IActionResult PrintTicket([FromBody] TicketRequest request)
        {
            try
            {
                string printerParam = request.Printer;

                // ... (código existente para determinar la impresora)

                Ticket ticket = new Ticket
                {
                    FOLIO = request.Folio,
                    KEY = request.Key,
                    NAME = request.Name,
                    ROOM = request.Room,
                    TYPE = request.Type,
                    OBS = request.Obs,
                    TROOM = request.Hostname,
                    PRINTERS = printerParam,
                    HOTEL = request.Hotel  
                };

                ticket.Print();
                _logger.LogInformation("Ticket {Folio} enviado a imprimir en {Printers} para hotel {Hotel}",
                    request.Folio, printerParam, request.Hotel);
                return Ok(new { success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al imprimir ticket {Folio}", request?.Folio);
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        // Método auxiliar para determinar la impresora basada en la reserva
        private string GetPrinterForReservation(TicketRequest request)
        {
            try
            {
                // Obtener el hotel de la reserva (aquí necesitas implementar la lógica específica)
                // Esta es una implementación de ejemplo - debes ajustarla según tus datos
                string hotel = DetermineHotelFromReservation(request);

                // Cargar la configuración de hoteles
                var configPath = Path.Combine(_env.ContentRootPath, "Data", HotelConfigFileName);
                if (!System.IO.File.Exists(configPath))
                {
                    _logger.LogWarning("No se encontró configuración de impresoras por hotel");
                    return null;
                }

                var json = System.IO.File.ReadAllText(configPath);
                var hotelConfigs = JsonSerializer.Deserialize<Dictionary<string, HotelPrinterConfig>>(json);

                if (hotelConfigs.TryGetValue(hotel, out var config))
                {
                    // Si está configurado para usar la configuración general o no tiene impresora específica
                    if (config.UseGeneral || string.IsNullOrWhiteSpace(config.Printer))
                    {
                        // Usar la primera configuración general disponible
                        var generalConfig = _configService.GetAll().FirstOrDefault();
                        return generalConfig?.Printers;
                    }

                    return config.Printer;
                }

                _logger.LogWarning("No se encontró configuración para el hotel: {Hotel}", hotel);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al determinar impresora para reserva");
                return null;
            }
        }

        // Método para determinar el hotel basado en la reserva (CON SIGLAS)
        private string DetermineHotelFromReservation(TicketRequest request)
        {
            // 1. Primero intentar por tipo de habitación (siglas)
            if (!string.IsNullOrEmpty(request.Type)) // Asumiendo que Type contiene el tipo de habitación
            {
                var siglas = request.Type.ToUpper().Trim();
                var hotelPorSiglas = DetermineHotelPorSiglas(siglas);

                if (hotelPorSiglas != "NULL")
                    return hotelPorSiglas;
            }

            // 2. Intentar por número de habitación (como respaldo)
            if (!string.IsNullOrEmpty(request.Room))
            {
                if (request.Room.StartsWith("1") || request.Room.StartsWith("A"))
                    return "The Grand Mayan";
                if (request.Room.StartsWith("2") || request.Room.StartsWith("B"))
                    return "The Grand Bliss";
                if (request.Room.StartsWith("3") || request.Room.StartsWith("C"))
                    return "The Grand Luxxe";
                if (request.Room.StartsWith("4") || request.Room.StartsWith("D"))
                    return "Mayan Palace";
            }

            return "NULL";
        }

        // Método auxiliar para mapear siglas a hoteles en C#
        private string DetermineHotelPorSiglas(string siglas)
        {
            var mapeoSiglas = new Dictionary<string, string>
    {
        // Grand Luxxe
        {"LVIMR", "The Grand Luxxe"}, {"LCOMR", "The Grand Luxxe"},
        {"LCOMS", "The Grand Luxxe"}, {"LCOST", "The Grand Luxxe"},
        {"LVIST", "The Grand Luxxe"}, {"LVIMS", "The Grand Luxxe"},
        {"LRL1B", "The Grand Luxxe"}, {"LRLCE", "The Grand Luxxe"},
        {"LPUSH", "The Grand Luxxe"}, {"LPU1B", "The Grand Luxxe"},
        {"LPU2B", "The Grand Luxxe"}, {"LPU3B", "The Grand Luxxe"},
        {"LSP2B", "The Grand Luxxe"}, {"LSP3B", "The Grand Luxxe"},
        {"LRL2B", "The Grand Luxxe"}, {"LRL3B", "The Grand Luxxe"},
        {"LRL4B", "The Grand Luxxe"}, {"LRD1B", "The Grand Luxxe"},
        {"LRD28", "The Grand Luxxe"},
        
        // Grand Bliss
        {"GBLMR", "The Grand Bliss"}, {"GBLST", "The Grand Bliss"},
        {"GBLMS", "The Grand Bliss"},
        
        // Grand Mayan
        {"GMAMR", "The Grand Mayan"}, {"GMAST", "The Grand Mayan"},
        {"GMAMS", "The Grand Mayan"},
        
        // Mayan Palace
        {"MPAMR", "Mayan Palace"}, {"MPAST", "Mayan Palace"},
        {"MPAMS", "Mayan Palace"},
        
        // Sea Garden
        {"SGAMR", "Sea Garden"}, {"SGAST", "Sea Garden"},
        
        // Bliss
        {"BLIMR", "Bliss"}, {"BLIST", "Bliss"}, {"BLIMS", "Bliss"},
        
        // The Estates
        {"EST1B", "The Estates"}, {"EST2B", "The Estates"},
        {"EST3B", "The Estates"}, {"EST4B", "The Estates"},
        
        // Celebrate Park
        {"CPAJS", "Celebrate Park"}, {"CPAJL", "Celebrate Park"},
        {"CPA2B", "Celebrate Park"},
        
        // Empire Estates / Kingdom of the Sun
        {"EEE4B", "Kingdom Of The Sun"}, {"KOSPST", "Kingdom Of The Sun"},
        {"KOS2PS", "Kingdom Of The Sun"}, {"BON1BK", "Kingdom Of The Sun"},
        {"BON1BD", "Kingdom Of The Sun"},
        
        // DeLuxxe (Grand Mayan)
        {"GMAD2B", "The Grand Mayan"}, {"GMAD1B", "The Grand Mayan"},
        {"GMADJS", "The Grand Mayan"}
    };

            return mapeoSiglas.ContainsKey(siglas) ? mapeoSiglas[siglas] : "NULL";
        }
    }

    // Clase para representar la configuración de impresora por hotel
    public class HotelPrinterConfig
    {
        public string Printer { get; set; }
        public bool UseGeneral { get; set; }
    }
}