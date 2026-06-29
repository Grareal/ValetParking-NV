using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AppValetParking.Data;
using AppValetParking.Models;
using ClosedXML.Excel;
using AppValetParking.Services;

namespace AppValetParking.Controllers
{
    public class ControlSolicitudesController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly PegasysDbContext _pegasysContext;

        public ControlSolicitudesController(ApplicationDbContext context, PegasysDbContext pegasysContext)
        {
            _context = context;
            _pegasysContext = pegasysContext;
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

            //valor de cada tiempo de entrega

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


        public IActionResult Pantalla()
        {
            return View();
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

        [HttpGet]
        [Route("api/solicitudes")]
        public async Task<IActionResult> ObtenerSolicitudes()
        {
            var solicitudes = await (
                from s in _context.ValetSolicitudes
                join v in _context.VehiculosInfo
                    on s.FolioVP equals v.FolioVP into vehiculos
                from v in vehiculos.DefaultIfEmpty()
                where s.TiempoAtendido == null
                orderby s.FechaSolicitud descending
                select new
                {
                    s.Id,
                    s.FolioVP,
                    s.NombreReserva,
                    s.Habitacion,
                    s.Resort,

                    // Datos del vehículo
                    Placas = v != null ? v.Placas : s.Placas,
                    Marca = v != null ? v.Marca : s.Marca,
                    Color = v != null ? v.Color : s.Color,
                    Modelo = v != null ? v.Modelo : null,

                    s.Posicion,
                    s.Estatus,
                    s.FechaSolicitud,

                    TiempoEspera = EF.Functions.DateDiffMinute(
                        s.FechaSolicitud,
                        DateTime.Now
                    )
                }
            ).ToListAsync();

            return Ok(solicitudes);
        }

        [HttpPost]
        [Route("api/solicitudes/tomar/{id}")]
        public async Task<IActionResult> TomarSolicitud(int id, string? numeroOperador)
        {
            var solicitud = await _context.ValetSolicitudes.FindAsync(id);

            if (solicitud == null)
                return NotFound();

            var now = DateTime.Now;

            string valetNombre = "Desconocido";
            if (!string.IsNullOrWhiteSpace(numeroOperador))
            {
                try
                {
                    var nombre = await _pegasysContext.VV_TARJETAS_EMPLEADOS
                        .Where(v => v.ID_ICLASS == numeroOperador)
                        .Select(v => v.c_mname + " " + v.c_lname)
                        .FirstOrDefaultAsync();
                    valetNombre = nombre?.Trim() ?? numeroOperador;
                }
                catch
                {
                    valetNombre = numeroOperador;
                }
            }

            solicitud.Estatus = "En proceso";
            solicitud.TiempoAtendido = now;

            var registro = !string.IsNullOrWhiteSpace(solicitud.FolioVP)
                ? await _context.ValetRegistros.FirstOrDefaultAsync(r => r.FolioVP == solicitud.FolioVP)
                : null;

            string tipo = !string.IsNullOrWhiteSpace(solicitud.TipoSalida) ? solicitud.TipoSalida! : "VEHÍCULO";
            string movimientoTexto = $"|ACEPTAR SOLICITUD {tipo} {now:yyyy-MM-dd HH:mm} por operador {valetNombre}|";

            var movimiento = new ValetMovimiento
            {
                IdRegistro = registro?.Id,
                Reserva = registro?.Reserva,
                Servicio = tipo,
                FechaHora = now,
                Operador = valetNombre,
                MovimientoTexto = movimientoTexto
            };

            _context.ValetMovimientos.Add(movimiento);

            if (registro != null)
                registro.Movimientos = (registro.Movimientos ?? "") + movimientoTexto;

            await _context.SaveChangesAsync();

            return Ok(new
            {
                success = true,
                mensaje = "Solicitud tomada"
            });
        }

        // ===================== EXPORTAR EXCEL ======================
        public IActionResult Excel(DateTime? inicio, DateTime? fin)
        {



            inicio ??= DateTime.Today;
            fin ??= DateTime.Today;

            var fechaInicio = inicio.Value.Date;
            var fechaFin = fin.Value.Date.AddDays(1); // 
            var data = _context.ValetSolicitudes
                .Where(s => s.FechaSolicitud >= fechaInicio && s.FechaSolicitud < fechaFin)
                .ToList();

            using var wb = new XLWorkbook();
            var headers = new[] { "Folio", "Huésped", "Habitación", "Hotel", "Fecha Solicitud", "Atendido", "Tiempo (minutos)" };
            var ws = ExcelExportHelper.CreateStyledSheet(wb, "Solicitudes", headers);

            int row = 2;

            foreach (var s in data)
            {
                ws.Cell(row, 1).Value = s.FolioVP;
                ws.Cell(row, 2).Value = s.NombreReserva;
                ws.Cell(row, 3).Value = s.Habitacion;
                ws.Cell(row, 4).Value = s.Resort;
                ws.Cell(row, 5).Value = s.FechaSolicitud;
                ws.Cell(row, 5).Style.DateFormat.Format = "yyyy-MM-dd HH:mm";

                if (s.TiempoAtendido != null)
                {
                    var tiempo = s.TiempoAtendido.Value - s.TiempoCreado;

                    ws.Cell(row, 6).Value = s.TiempoAtendido.Value;
                    ws.Cell(row, 6).Style.DateFormat.Format = "yyyy-MM-dd HH:mm";
                    ws.Cell(row, 7).Value = $"{(int)tiempo.TotalHours}h {tiempo.Minutes}m";
                }
                else
                {
                    ws.Cell(row, 6).Value = "Pendiente";
                }

                row++;
            }

            ExcelExportHelper.FinalizeStyledSheet(ws, row - 1, headers.Length);

            using var stream = new MemoryStream();
            wb.SaveAs(stream);

            return File(stream.ToArray(),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"ReporteSolicitudes_{DateTime.Today:yyyyMMdd}.xlsx");
        }

    }
}
