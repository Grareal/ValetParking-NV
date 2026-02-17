using Microsoft.AspNetCore.Mvc;
using AppValetParking.Data;
using AppValetParking.Models;
using System.Linq;
using System.Threading.Tasks;
using System.Text.Json;
using System.IO;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;


namespace AppValetParking.Controllers
{
    public class OperadoraController : Controller
    {
        private readonly ApplicationDbContext _contextApp;
        private readonly PegasysDbContext _contextPegasys;

        public OperadoraController(ApplicationDbContext contextApp, PegasysDbContext contextPegasys)
        {
            _contextApp = contextApp;
            _contextPegasys = contextPegasys;
        }

        public IActionResult Index(DateTime? fechaInicio, DateTime? fechaFin, string buscar, int pagina = 1)
        {
            int pageSize = 20;

            var query = _contextApp.ValetRegistros
                .Where(r => r.Situacion == "USADO")
                .AsNoTracking()
                .AsQueryable();

            if (fechaInicio.HasValue)
                query = query.Where(r => r.Fecha >= fechaInicio.Value);

            if (fechaFin.HasValue)
                query = query.Where(r => r.Fecha <= fechaFin.Value);

            //  BÚSQUEDA GLOBAL EN TODA LA TABLA
            if (!string.IsNullOrWhiteSpace(buscar))
            {
                buscar = buscar.Trim();

                query = query.Where(r =>
                    (r.FolioVP != null && r.FolioVP.Contains(buscar)) ||
                    (r.Habitacion != null && r.Habitacion.Contains(buscar)) ||
                    (r.Reserva != null && r.Reserva.Contains(buscar)) ||
                    (r.Hotel != null && r.Hotel.Contains(buscar)) ||
                    (r.Valet != null && r.Valet.Contains(buscar))
                );
            }

            int totalRegistros = query.Count();

            var registros = query
                .OrderByDescending(r => r.Fecha)
                .ThenByDescending(r => r.Solicitud)
                .Skip((pagina - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            ViewBag.PaginaActual = pagina;
            ViewBag.TotalPaginas = (int)Math.Ceiling((double)totalRegistros / pageSize);

            ViewBag.FechaInicio = fechaInicio?.ToString("yyyy-MM-dd");
            ViewBag.FechaFin = fechaFin?.ToString("yyyy-MM-dd");
            ViewBag.Buscar = buscar;

            // Servicios JSON (con fix null)
            string rutaJson = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "Config", "servicios.json");
            List<ServicioItem> servicios = new();

            if (System.IO.File.Exists(rutaJson))
            {
                var json = System.IO.File.ReadAllText(rutaJson);
                var opciones = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                servicios = JsonSerializer.Deserialize<List<ServicioItem>>(json, opciones) ?? new List<ServicioItem>();
            }

            ViewBag.Servicios = servicios;

            return View("OperadoraView", registros);
        }


        [HttpPost]
        public async Task<IActionResult> ExportarExcel(DateTime fechaInicio, DateTime fechaFin)
        {
            var registros = await Task.Run(() =>
                    _contextApp.ValetRegistros
                    .Where(r => r.Fecha >= fechaInicio && r.Fecha <= fechaFin)
                    .OrderBy(r => r.Fecha)
                    .ToList()
            );

            using var workbook = new ClosedXML.Excel.XLWorkbook();
            var worksheet = workbook.Worksheets.Add("Registros");

            // Encabezados
            worksheet.Cell(1, 1).Value = "Fecha";
            worksheet.Cell(1, 2).Value = "Operadora";
            worksheet.Cell(1, 3).Value = "H. Solicitud";
            worksheet.Cell(1, 4).Value = "Habitación";
            worksheet.Cell(1, 5).Value = "Hotel";
            worksheet.Cell(1, 6).Value = "Reserva";
            worksheet.Cell(1, 7).Value = "FolioVP";
            worksheet.Cell(1, 8).Value = "Codigo Gafette";
            worksheet.Cell(1, 9).Value = "Nombre Valet";
            worksheet.Cell(1, 10).Value = "Servicio";
            worksheet.Cell(1, 11).Value = "H. Ultimo Mov";
            worksheet.Cell(1, 12).Value = "Cajón";

            int row = 2;
            foreach (var r in registros)
            {
                worksheet.Cell(row, 1).Value = r.Fecha.ToString("yyyy-MM-dd");
                worksheet.Cell(row, 2).Value = r.Operacion ?? "";
                worksheet.Cell(row, 3).Value = r.Solicitud?.ToString(@"hh\:mm\:ss") ?? "";
                worksheet.Cell(row, 4).Value = r.Habitacion ?? "";
                worksheet.Cell(row, 5).Value = r.Hotel ?? "";
                worksheet.Cell(row, 6).Value = r.Reserva ?? "";
                worksheet.Cell(row, 7).Value = r.FolioVP ?? "";
                worksheet.Cell(row, 8).Value = r.NumeroOperador ?? "";
                worksheet.Cell(row, 9).Value = r.Valet ?? "";
                worksheet.Cell(row, 10).Value = r.Servicio ?? "";
                worksheet.Cell(row, 11).Value = r.HoraSalida?.ToString(@"hh\:mm") ?? "";
                worksheet.Cell(row, 12).Value = r.CajonBuffer ?? "";

                row++;
            }

            var stream = new MemoryStream();
            workbook.SaveAs(stream);
            stream.Position = 0;

            return File(
                stream,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"Registros_{fechaInicio:yyyyMMdd}_{fechaFin:yyyyMMdd}.xlsx"
            );
        }

        [HttpPost]
        public async Task<IActionResult> LiberarFolioVP(string folioVP)
        {
            if (string.IsNullOrWhiteSpace(folioVP))
                return RedirectToAction("Index");

            var registro = _contextApp.ValetRegistros
                .FirstOrDefault(r => r.FolioVP == folioVP);

            if (registro == null)
            {
                TempData["Error"] = "El FolioVP no existe.";
                return RedirectToAction("Index");
            }

            // También eliminar movimientos relacionados si existen
            var movimientos = _contextApp.ValetMovimientos
                .Where(m => m.IdRegistro == registro.Id);

            _contextApp.ValetMovimientos.RemoveRange(movimientos);
            _contextApp.ValetRegistros.Remove(registro);

            await _contextApp.SaveChangesAsync();

            TempData["Success"] = $"Folio {folioVP} liberado correctamente.";
            return RedirectToAction("Index");
        }


        [HttpPost]
        public async Task<IActionResult> ActualizarRegistro(ValetRegistro registro)
        {
            var registroDb = _contextApp.ValetRegistros.Find(registro.Id);

            if (registroDb == null)
                return NotFound();

            var cambios = new List<string>();

            if (registroDb.Valet != registro.Valet)
                cambios.Add($"Operadora cambiada de '{registroDb.Valet}' a '{registro.Valet}'");

            if (registroDb.Servicio != registro.Servicio)
                cambios.Add($"Servicio cambiado de '{registroDb.Servicio}' a '{registro.Servicio}'");

            if (registroDb.CajonBuffer != registro.CajonBuffer)
                cambios.Add($"Cajón Buffer cambiado de '{registroDb.CajonBuffer}' a '{registro.CajonBuffer}'");

            if (registroDb.Operacion != registro.Operacion)
                cambios.Add($"Operación cambiada de '{registroDb.Operacion}' a '{registro.Operacion}'");

            if (TimeSpan.TryParse(Request.Form["HoraSalida"], out var horaSalida))
            {
                if (registroDb.HoraSalida != horaSalida)
                    cambios.Add($"Hora de salida cambiada de '{registroDb.HoraSalida}' a '{horaSalida}'");

                registroDb.HoraSalida = horaSalida;
            }
            else
            {
                if (registroDb.HoraSalida != null)
                    cambios.Add($"Hora de salida eliminada");

                registroDb.HoraSalida = null;
            }

            registroDb.Valet = registro.Valet;
            registroDb.Servicio = registro.Servicio;
            registroDb.CajonBuffer = registro.CajonBuffer;
            registroDb.Operacion = registro.Operacion;

            await _contextApp.SaveChangesAsync();

            if (cambios.Any())
            {
                var textoMovimiento = "Se actualizaron los siguientes campos: " + string.Join("; ", cambios) + $" ({DateTime.Now:yyyy-MM-dd HH:mm})";

                var movimiento = new ValetMovimiento
                {
                    IdRegistro = registroDb.Id,
                    FechaHora = DateTime.Now,
                    Operador = registroDb.Operacion,
                    Servicio = registroDb.Servicio,
                    MovimientoTexto = textoMovimiento
                };

                _contextApp.ValetMovimientos.Add(movimiento);
                await _contextApp.SaveChangesAsync();
            }

            return RedirectToAction("Index");
        }

        [HttpGet]
        [Route("api/Empleados/BuscarPorCodigo")]
        public IActionResult BuscarPorCodigo(string codigo)
        {
            if (string.IsNullOrWhiteSpace(codigo))
                return BadRequest("Código vacío");

            var empleado = _contextPegasys.VV_TARJETAS_EMPLEADOS
                .FirstOrDefault(e =>
                    e.clavenomina == codigo ||
                    e.ID_ICLASS == codigo ||
                    e.ID_MIFARE == codigo
                );

            if (empleado == null)
                return NotFound();

            return Json(new
            {
                nombreCompleto = $"{empleado.c_mname} {empleado.c_lname}"
            });
        }

        [HttpGet]
        public IActionResult ObtenerMovimientos(int idRegistro)
        {
            var movimientos = _contextApp.ValetMovimientos
                .Where(m => m.IdRegistro == idRegistro)
                .OrderByDescending(m => m.FechaHora)
                .Select(m => new
                {
                    m.FechaHora,
                    m.Operador,
                    m.Servicio,
                    m.MovimientoTexto
                })
                .ToList();

            return Json(movimientos);
        }

        [HttpPost]
        public async Task<IActionResult> ActualizarHostname(int id, string HOSTNAME)
        {
            var registroDb = _contextApp.ValetRegistros.Find(id);

            if (registroDb == null)
                return NotFound();

            registroDb.HOSTNAME = HOSTNAME;
            await _contextApp.SaveChangesAsync();

            return RedirectToAction("Index");
        }

        [HttpPost]
        public async Task<IActionResult> ActualizarEstatus(int id, string nuevoEstatus)
        {
            var registroDb = _contextApp.ValetRegistros.Find(id);

            if (registroDb == null)
                return NotFound();

            registroDb.Estatus = nuevoEstatus;
            await _contextApp.SaveChangesAsync();

            return RedirectToAction("Index");
        }
    }
}
