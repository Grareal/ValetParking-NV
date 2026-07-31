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
using QRCoder;

namespace AppValetParking.Controllers
{
    public class TicketsController : Controller
    {
        private readonly ValetParkingDbContext _valetContext;
        private readonly ApplicationDbContext _context;
        private readonly TcabdopeNewDbContext _tcabdopeContext;
        private readonly PegasysDbContext _pegasysContext;
        private readonly IWebHostEnvironment _env;
        private readonly ILogger<TicketsController> _logger;

        private const string RulesConfigFile = "ticket-rules.json";
        private const string PrinterConfigFile = "printer-config.json";

        public TicketsController(
    ValetParkingDbContext valetContext,
    ApplicationDbContext context,
    TcabdopeNewDbContext tcabdopeContext,
    PegasysDbContext pegasysContext,
    IWebHostEnvironment env,
    ILogger<TicketsController> logger)
        {
            _valetContext = valetContext;
            _context = context;
            _tcabdopeContext = tcabdopeContext;
            _pegasysContext = pegasysContext;
            _env = env;
            _logger = logger;
        }

        // Resuelve el operador del usuario web logueado: su gafete (desde
        // Usuarios) y su nombre (desde el padrón Pegasys). Si algo falla, cae al
        // Username de la sesión.
        private async Task<(string codigoOperador, string nombre)> ResolverOperadorSesionAsync()
        {
            var username = HttpContext.Session.GetString("Usuario") ?? "";
            var gafete = await _context.Usuarios
                .Where(u => u.Username == username)
                .Select(u => u.Gafete)
                .FirstOrDefaultAsync() ?? "";

            string nombre = username;
            if (!string.IsNullOrWhiteSpace(gafete))
            {
                try
                {
                    var n = await _pegasysContext.VV_TARJETAS_EMPLEADOS
                        .Where(e => e.ID_ICLASS == gafete
                                 || e.clavenomina == gafete
                                 || e.ID_MIFARE == gafete)
                        .Select(e => (e.c_mname + " " + e.c_lname).Trim())
                        .FirstOrDefaultAsync();
                    if (!string.IsNullOrWhiteSpace(n)) nombre = n;
                }
                catch { /* Pegasys no disponible: se queda con el username */ }
            }
            return (gafete, nombre);
        }

        // Genera un PNG con el QR del texto dado (p. ej. el código de la
        // operadora). Se usa desde la vista Config para imprimir/mostrar los QR
        // con los que se cierran los ciclos de estacionado.
        // PngByteQRCode es multiplataforma (no usa System.Drawing).
        [HttpGet]
        public IActionResult GenerarQR(string texto)
        {
            if (string.IsNullOrWhiteSpace(texto))
                return BadRequest("Texto vacío");

            using var gen = new QRCodeGenerator();
            var data = gen.CreateQrCode(texto.Trim(), QRCodeGenerator.ECCLevel.Q);
            var png = new PngByteQRCode(data).GetGraphic(10);
            return File(png, "image/png");
        }

        // Lista de códigos de liberación (para la vista Config).
        [HttpGet]
        public IActionResult ListaCodigos()
        {
            var codigos = _context.CodigosLiberacion
                .OrderByDescending(c => c.Fecha)
                .Select(c => new { c.Id, c.Codigo, c.Nombre, c.CodigoOperador, c.Activo, c.Fecha, c.ExpiraEn })
                .ToList();
            return Json(codigos);
        }

        // Borrado LÓGICO (conserva el histórico): el código queda "Borrado" y ya
        // no valida, pero sigue visible para consulta.
        [HttpPost]
        public async Task<IActionResult> EliminarCodigo(int id)
        {
            var c = await _context.CodigosLiberacion.FindAsync(id);
            if (c == null) return Ok(new { success = false, mensaje = "No encontrado." });
            c.Activo = false;
            await _context.SaveChangesAsync();
            return Ok(new { success = true });
        }

        // Reactiva un código previamente borrado.
        [HttpPost]
        public async Task<IActionResult> RestaurarCodigo(int id)
        {
            var c = await _context.CodigosLiberacion.FindAsync(id);
            if (c == null) return Ok(new { success = false, mensaje = "No encontrado." });
            c.Activo = true;
            await _context.SaveChangesAsync();
            return Ok(new { success = true });
        }

        // Fija (o quita) la vigencia de un código. expira vacío = sin caducidad.
        [HttpPost]
        public async Task<IActionResult> SetVigencia(int id, string? expira)
        {
            var c = await _context.CodigosLiberacion.FindAsync(id);
            if (c == null) return Ok(new { success = false, mensaje = "No encontrado." });
            if (string.IsNullOrWhiteSpace(expira))
                c.ExpiraEn = null;
            else if (DateTime.TryParse(expira, out var dt))
                c.ExpiraEn = dt;
            else
                return Ok(new { success = false, mensaje = "Fecha inválida." });
            await _context.SaveChangesAsync();
            return Ok(new { success = true, expiraEn = c.ExpiraEn });
        }

        // Crea un código propio vinculado a un nombre. Ese nombre aparecerá en
        // movimientos como quien aprobó cuando se use el código.
        [HttpPost]
        public async Task<IActionResult> CrearCodigo([FromBody] CodigoLiberacion dto)
        {
            if (dto == null || string.IsNullOrWhiteSpace(dto.Codigo))
                return BadRequest(new { success = false, mensaje = "El código es obligatorio." });

            var codigo = dto.Codigo.Trim();
            var existe = await _context.CodigosLiberacion.AnyAsync(c => c.Codigo == codigo);
            if (existe)
                return Ok(new { success = false, mensaje = "Ese código ya existe." });

            // Autocaptura del operador logueado (código + nombre real). El código
            // queda ligado a esa persona: al usarse, en movimientos sale que
            // ELLA autorizó.
            var (codigoOperador, nombre) = await ResolverOperadorSesionAsync();

            _context.CodigosLiberacion.Add(new CodigoLiberacion
            {
                Codigo = codigo,
                Nombre = nombre,
                CodigoOperador = codigoOperador,
                Activo = true,
                CreadoPor = HttpContext.Session.GetString("Usuario"),
                Fecha = DateTime.Now,
                ExpiraEn = dto.ExpiraEn   // opcional: vigencia al crear (o null = sin caducidad)
            });
            await _context.SaveChangesAsync();
            return Ok(new { success = true, nombre, codigoOperador });
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
        // BUSCAR RESERVAS DEL DÍA (server-side, bajo demanda)
        // ==========================================
        // Evita descargar todas las reservas del día al abrir la pantalla de
        // Reservación: solo consulta la BD cuando el usuario ya escribió algo.
        [HttpGet]
        // NOTA DE RENDIMIENTO: dbo.hothsp2 tiene ~1.24M filas y NINGÚN índice,
        // así que toda consulta la recorre completa (medido: ~1-2 s caliente;
        // fría, con la tabla fuera de caché, se va a decenas de segundos). Por
        // eso se evita '%q%' donde se puede: un Like de prefijo ('q%') cuesta
        // como la mitad que uno con comodín al inicio. La cura de fondo sería
        // un índice sobre h_res_cve / h_cod_reserva (decisión del DBA del
        // hotel: la BD es TCADBOPE, no nuestra).
        //
        // soloClaves = búsqueda por número de reserva / TSW y nada más. Lo usa
        // la pantalla Reservación: buscar por habitación traía decenas de
        // coincidencias parecidas y se prestaba a confusión.
        public async Task<IActionResult> BuscarReservasDia(string? q, bool todas = false, bool soloClaves = false)
        {
            if (string.IsNullOrWhiteSpace(q) || q.Trim().Length < 2)
                return Json(new List<object>());

            var query = q.Trim();
            var fechaHoy = DateTime.Today.ToString("yyyyMMdd");

            // El huésped llega indistintamente con el número de reserva
            // (h_res_cve, 9 dígitos) o con el TSW (h_cod_reserva, 7).
            var esDigitos = query.All(char.IsDigit);

            var baseQuery = _tcabdopeContext.Reservas
                .Where(r =>
                    !string.IsNullOrEmpty(r.h_res_cve) &&
                    !string.IsNullOrEmpty(r.h_cod_reserva));

            if (soloClaves)
            {
                // Solo dígitos: además de ser lo único que tiene sentido aquí,
                // evita que un '%' o '_' tecleado entre como comodín al Like.
                if (!esDigitos)
                    return Json(new List<object>());

                // Prefijo: se busca conforme se teclea (2+ dígitos) sin obligar
                // a escribir el número completo. Sin filtro de fecha: si tienes
                // la clave, la reserva que quieres es esa, sea de hoy o no.
                baseQuery = baseQuery.Where(r =>
                    EF.Functions.Like(r.h_res_cve, $"{query}%") ||
                    EF.Functions.Like(r.h_cod_reserva, $"{query}%"));
            }
            else if (esDigitos && query.Length >= 6)
            {
                // Clave completa (una habitación nunca llega a 6 dígitos):
                // igualdad, en todo el historial.
                baseQuery = baseQuery.Where(r =>
                    r.h_res_cve == query || r.h_cod_reserva == query);
            }
            else
            {
                baseQuery = baseQuery.Where(r =>
                    EF.Functions.Like(r.h_nom, $"%{query}%") ||
                    EF.Functions.Like(r.h_num_hab, $"%{query}%"));

                // El filtro por fecha solo aplica a nombre/habitación.
                if (!todas)
                    baseQuery = baseQuery.Where(r => r.h_fec_lld == fechaHoy);
            }

            var reservas = await baseQuery
                .OrderByDescending(r => r.h_fec_lld)
                .ThenBy(r => r.Hotel)
                .ThenBy(r => r.h_num_hab)
                .Select(r => new
                {
                    h_status = r.h_status,
                    r.h_res_cve,
                    r.h_nom,
                    r.h_num_hab,
                    r.Hotel,
                    r.h_tpo_hab,
                    r.h_tpo_hsp,
                    r.m_msg0,
                    r.h_cod_reserva,
                    r.h_vip,
                    r.h_fec_lld
                })
                .Take(todas ? 50 : 30)
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
        public async Task<IActionResult> Config()
        {
            // No necesitas validar sesión ni permisos manualmente
            ViewBag.Permisos = HttpContext.Session.GetString("Permisos") ?? "";
            ViewBag.Usuario = HttpContext.Session.GetString("Usuario") ?? "";
            // Operador logueado (gafete + nombre real) al que se ligarán los códigos.
            var (codigoOperador, nombre) = await ResolverOperadorSesionAsync();
            ViewBag.OperadorCodigo = codigoOperador;
            ViewBag.OperadorNombre = nombre;
            return View();
        }

        // Vista dedicada SOLO a los códigos de liberación (separada de Config,
        // que ahora solo tiene las reglas de impresión). Reutiliza los mismos
        // endpoints JSON (ListaCodigos, CrearCodigo, etc.).
        [HttpGet]
        [Permiso("Configuracion")]
        public async Task<IActionResult> Codigos()
        {
            ViewBag.Permisos = HttpContext.Session.GetString("Permisos") ?? "";
            ViewBag.Usuario = HttpContext.Session.GetString("Usuario") ?? "";
            // Operador logueado (gafete + nombre real) al que se ligarán los códigos.
            var (codigoOperador, nombre) = await ResolverOperadorSesionAsync();
            ViewBag.OperadorCodigo = codigoOperador;
            ViewBag.OperadorNombre = nombre;
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

                // Crear solicitud "REGISTRAR" para que aparezca en la cola de
                // Solicitudes en cuanto se envía el ticket.
                _context.ValetSolicitudes.Add(new ValetSolicitud
                {
                    FolioVP = request.Folio,
                    NombreReserva = request.Name,
                    Habitacion = request.Room,
                    Resort = request.Hotel,
                    TipoSalida = "REGISTRAR",
                    FechaSolicitud = DateTime.Now,
                });
                await _context.SaveChangesAsync();

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