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
        private readonly TcabdopeNewDbContext _tcabdopeContext;

        public ControlSolicitudesController(ApplicationDbContext context, PegasysDbContext pegasysContext, TcabdopeNewDbContext tcabdopeContext)
        {
            _context = context;
            _pegasysContext = pegasysContext;
            _tcabdopeContext = tcabdopeContext;
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
            // Incluye solicitudes pendientes (TiempoAtendido == null) y las
            // que están "En proceso" o "Por entregar" (TiempoAtendido puede
            // haberse registrado al tomar, pero se mantienen visibles por Estatus).
            var solicitudes = await (
                from s in _context.ValetSolicitudes
                join v in _context.VehiculosInfo
                    on s.FolioVP equals v.FolioVP into vehiculos
                from v in vehiculos.DefaultIfEmpty()
                where s.TiempoAtendido == null
                   || s.Estatus == "En proceso"
                   || s.Estatus == "Por entregar"
                   || s.Estatus == "En paseo"
                orderby s.FechaSolicitud descending
                select new
                {
                    s.Id,
                    s.FolioVP,
                    s.NombreReserva,
                    s.Habitacion,
                    s.Resort,

                    Placas  = v != null ? v.Placas : s.Placas,
                    Marca   = v != null ? v.Marca  : (s.Marca ?? s.MarcaVehiculo),
                    Color   = v != null ? v.Color  : (s.Color ?? s.ColorVehiculo),
                    Modelo  = v != null ? v.Modelo : (string?)null,

                    // Cajón real: si el vehículo ya está estacionado, su cajón vive
                    // en ValetRegistro.CajonBuffer (ej. "A4", "BUFFER5"). Se toma el
                    // registro más reciente del folio que tenga cajón asignado; si no
                    // hay, cae al Posicion de la propia solicitud.
                    Posicion = _context.ValetRegistros
                        .Where(r => r.FolioVP == s.FolioVP
                                 && r.CajonBuffer != null
                                 && r.CajonBuffer != "")
                        .OrderByDescending(r => r.Id)
                        .Select(r => r.CajonBuffer)
                        .FirstOrDefault() ?? s.Posicion,
                    s.Estatus,
                    s.TipoSalida,
                    s.EstadoPaso,
                    s.FechaPendienteQr,
                    // PorEntregar calculado en memoria desde Estatus
                    PorEntregar = s.Estatus == "Por entregar",
                    // Lock: quién tomó la tarea (bloqueo visible en la app)
                    s.TomadoPor,
                    s.TomadoPorId,
                    Bloqueada = s.TomadoPorId != null && s.TomadoPorId != "",
                    s.FechaSolicitud,

                    TiempoEspera = EF.Functions.DateDiffMinute(
                        s.FechaSolicitud,
                        DateTime.Now
                    )
                }
            ).ToListAsync();

            // Checkout solo se expone/alerta cuando corresponde al dia actual.
            // Se consulta por lote para evitar una consulta TCADBOPE por tarjeta.
            var folios = solicitudes.Select(s => s.FolioVP).Where(f => !string.IsNullOrWhiteSpace(f)).Distinct().ToList();
            var registrosConReserva = await _context.ValetRegistros.AsNoTracking()
                .Where(r => folios.Contains(r.FolioVP) && r.Reserva != null && r.Reserva != "")
                .Select(r => new { r.Id, r.FolioVP, r.Reserva })
                .ToListAsync();
            var clavesPorFolio = registrosConReserva
                .GroupBy(r => r.FolioVP!, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(r => r.Id).First().Reserva!, StringComparer.OrdinalIgnoreCase);

            var claves = clavesPorFolio.Values.Distinct().ToList();
            var hoyPms = DateTime.Today.ToString("yyyyMMdd");
            var checkoutRows = claves.Count == 0
                ? []
                : await _tcabdopeContext.Reservas.AsNoTracking()
                    .Where(r => (claves.Contains(r.h_res_cve!) || claves.Contains(r.h_cod_reserva!)) && r.h_fec_sda == hoyPms)
                    .Select(r => new { r.h_res_cve, r.h_cod_reserva, r.h_fec_sda })
                    .ToListAsync();
            var checkoutPorClave = checkoutRows
                .SelectMany(r => new[] { r.h_res_cve, r.h_cod_reserva }
                    .Where(k => !string.IsNullOrWhiteSpace(k))
                    .Select(k => new { Clave = k!, Fecha = r.h_fec_sda! }))
                .GroupBy(x => x.Clave, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First().Fecha, StringComparer.OrdinalIgnoreCase);

            var respuesta = solicitudes.Select(s =>
            {
                var tipo = (s.TipoSalida ?? "").Trim().ToUpperInvariant();
                var esPaseo = tipo == "PASEO" || tipo == "PARCIAL";
                string? checkout = null;
                if (esPaseo && !string.IsNullOrWhiteSpace(s.FolioVP)
                    && clavesPorFolio.TryGetValue(s.FolioVP, out var clave)
                    && checkoutPorClave.TryGetValue(clave, out var fecha))
                    checkout = fecha;

                return new
                {
                    s.Id, s.FolioVP, s.NombreReserva, s.Habitacion, s.Resort,
                    s.Placas, s.Marca, s.Color, s.Modelo, s.Posicion,
                    s.Estatus, s.TipoSalida, s.PorEntregar, s.TomadoPor,
                    s.TomadoPorId, s.Bloqueada, s.FechaSolicitud, s.TiempoEspera,
                    s.EstadoPaso, s.FechaPendienteQr,
                    CheckoutHoy = checkout != null,
                    CheckoutFecha = checkout == null ? null : $"{checkout[6..8]}/{checkout[4..6]}/{checkout[0..4]}"
                };
            });

            return Ok(respuesta);
        }

        [HttpPost]
        [Route("api/solicitudes/tomar/{id}")]
        public async Task<IActionResult> TomarSolicitud(int id, string? numeroOperador)
        {
            var solicitud = await _context.ValetSolicitudes.FindAsync(id);

            if (solicitud == null)
                return NotFound();

            var now = DateTime.Now;

            // El valet solo se bloquea cuando ya tiene un estacionamiento con
            // cajon asignado y pendiente de validar QR.
            if (!string.IsNullOrWhiteSpace(numeroOperador))
            {
                var activa = await _context.ValetSolicitudes.AsNoTracking()
                    .Where(s => s.Id != id && s.TomadoPorId == numeroOperador
                        && s.EstadoPaso == "PENDIENTE_QR")
                    .Select(s => new { s.Id, s.FolioVP, s.EstadoPaso })
                    .FirstOrDefaultAsync();
                if (activa != null)
                {
                    return Ok(new
                    {
                        success = false,
                        tieneTrabajoActivo = true,
                        solicitudActivaId = activa.Id,
                        folioActivo = activa.FolioVP,
                        estadoActivo = activa.EstadoPaso,
                        mensaje = $"Tienes pendiente el QR del folio {activa.FolioVP}. Completa ese proceso antes de tomar otra solicitud."
                    });
                }
            }

            // ── LOCK ──────────────────────────────────────────────
            // Si otro operador ya la tomó, se rechaza (no se puede robar la
            // tarea). Si el mismo operador la re-toma, se permite (idempotente).
            if (!string.IsNullOrEmpty(solicitud.TomadoPorId) &&
                solicitud.TomadoPorId != numeroOperador)
            {
                return Ok(new
                {
                    success = false,
                    bloqueada = true,
                    tomadoPor = solicitud.TomadoPor,
                    mensaje = $"Esta solicitud ya la tomó {solicitud.TomadoPor ?? "otro operador"}."
                });
            }

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
            solicitud.TomadoPor = valetNombre;
            solicitud.TomadoPorId = numeroOperador;
            solicitud.FechaTomado = now;
            solicitud.EstadoPaso = "EN_PROCESO";

            var registro = !string.IsNullOrWhiteSpace(solicitud.FolioVP)
                ? await _context.ValetRegistros.FirstOrDefaultAsync(r => r.FolioVP == solicitud.FolioVP)
                : null;

            string tipo = !string.IsNullOrWhiteSpace(solicitud.TipoSalida) ? solicitud.TipoSalida! : "VEHÍCULO";
            string movimientoTexto = $"|ACEPTAR SOLICITUD {tipo} {now:yyyy-MM-dd HH:mm} por operador {valetNombre}|";

            if (registro != null)
            {
                var movimiento = new ValetMovimiento
                {
                    IdRegistro = registro.Id,
                    Reserva = registro.Reserva,
                    Servicio = tipo,
                    FechaHora = now,
                    Operador = valetNombre,
                    MovimientoTexto = movimientoTexto
                };
                _context.ValetMovimientos.Add(movimiento);
                registro.Movimientos = (registro.Movimientos ?? "") + movimientoTexto;
            }

            await _context.SaveChangesAsync();

            return Ok(new
            {
                success = true,
                tomadoPor = valetNombre,
                mensaje = "Solicitud tomada"
            });
        }

        // ===================== ENTREGAR AL HUÉSPED ================
        [HttpPost]
        [Route("api/solicitudes/entregar/{id}")]
        public async Task<IActionResult> EntregarSolicitud(int id, string? numeroOperador)
        {
            var solicitud = await _context.ValetSolicitudes.FindAsync(id);
            if (solicitud == null)
                return NotFound(new { success = false });

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

            string tipo = (solicitud.TipoSalida ?? "").Trim().ToUpperInvariant();

            var registro = !string.IsNullOrWhiteSpace(solicitud.FolioVP)
                ? await _context.ValetRegistros.FirstOrDefaultAsync(r => r.FolioVP == solicitud.FolioVP)
                : null;
            var vehiculo = !string.IsNullOrWhiteSpace(solicitud.FolioVP)
                ? await _context.VehiculosInfo.FirstOrDefaultAsync(v => v.FolioVP == solicitud.FolioVP)
                : null;

            bool esPaseo = tipo == "PASEO" || tipo == "PARCIAL";
            bool esDefinitiva = tipo == "SALIDA" || tipo == "PERMANENTE";
            string movimientoTexto;

            // El estatus cambia AQUÍ (al confirmar con el click), no al crear
            // la solicitud.
            if (esDefinitiva)
            {
                // Salida definitiva: el registro "sale" — Situacion USADO -> SALIDA.
                if (registro != null) { registro.Situacion = "SALIDA"; registro.Servicio = "SALIDA"; }
                if (vehiculo != null) vehiculo.Estatus = "Fuera";
                solicitud.Estatus = "Entregado"; // cierra: desaparece de la lista
                movimientoTexto = $"|SALIDA DEFINITIVA {now:yyyy-MM-dd HH:mm} por operador {valetNombre}|";
            }
            else if (esPaseo)
            {
                // Paseo: el coche salió pero VUELVE. La solicitud se queda como
                // recordatorio ("En paseo") hasta que el huésped regrese.
                if (registro != null) registro.Servicio = "PASEO";
                if (vehiculo != null) vehiculo.Estatus = "Parcial";
                solicitud.Estatus = "En paseo"; // NO se cierra
                movimientoTexto = $"|SALIDA DE PASEO {now:yyyy-MM-dd HH:mm} por operador {valetNombre}|";
            }
            else
            {
                solicitud.Estatus = "Entregado";
                movimientoTexto = $"|ENTREGA VEHÍCULO {now:yyyy-MM-dd HH:mm} por operador {valetNombre}|";
            }

            solicitud.TiempoAtendido ??= now;

            if (registro != null)
            {
                _context.ValetMovimientos.Add(new ValetMovimiento
                {
                    IdRegistro = registro.Id,
                    Reserva = registro.Reserva,
                    Servicio = tipo,
                    FechaHora = now,
                    Operador = valetNombre,
                    MovimientoTexto = movimientoTexto
                });
                registro.Movimientos = (registro.Movimientos ?? "") + movimientoTexto;
            }

            await _context.SaveChangesAsync();

            return Ok(new
            {
                success = true,
                estatus = solicitud.Estatus,
                mensaje = esPaseo
                    ? "Paseo registrado. La solicitud queda como recordatorio hasta el regreso."
                    : "Entrega confirmada."
            });
        }

        // ============ REGRESO DE PASEO → CONTINUAR A ESTACIONAR ====
        // El huésped regresó de su paseo: se cierra la solicitud "En paseo" y
        // se crea una solicitud ESTACIONAR para volver a estacionar el coche.
        [HttpPost]
        [Route("api/solicitudes/regresoPaseo/{id}")]
        public async Task<IActionResult> RegresoPaseo(int id, string? numeroOperador)
        {
            var solicitud = await _context.ValetSolicitudes.FindAsync(id);
            if (solicitud == null)
                return NotFound(new { success = false });

            var now = DateTime.Now;

            string valetNombre = numeroOperador ?? "Desconocido";
            if (!string.IsNullOrWhiteSpace(numeroOperador))
            {
                try
                {
                    var n = await _pegasysContext.VV_TARJETAS_EMPLEADOS
                        .Where(v => v.ID_ICLASS == numeroOperador)
                        .Select(v => v.c_mname + " " + v.c_lname)
                        .FirstOrDefaultAsync();
                    valetNombre = n?.Trim() ?? numeroOperador;
                }
                catch
                {
                    valetNombre = numeroOperador;
                }
            }

            var registro = !string.IsNullOrWhiteSpace(solicitud.FolioVP)
                ? await _context.ValetRegistros.FirstOrDefaultAsync(r => r.FolioVP == solicitud.FolioVP)
                : null;
            var vehiculo = !string.IsNullOrWhiteSpace(solicitud.FolioVP)
                ? await _context.VehiculosInfo.FirstOrDefaultAsync(v => v.FolioVP == solicitud.FolioVP)
                : null;

            if (vehiculo != null) vehiculo.Estatus = "Dentro";

            // Cierra la solicitud de paseo.
            solicitud.Estatus = "Entregado";
            solicitud.TiempoAtendido ??= now;

            // Nueva solicitud ESTACIONAR: el coche vuelve al ciclo de estacionar.
            _context.ValetSolicitudes.Add(new ValetSolicitud
            {
                FolioVP = solicitud.FolioVP,
                NombreReserva = solicitud.NombreReserva,
                Habitacion = solicitud.Habitacion,
                Resort = solicitud.Resort,
                TipoSalida = "ESTACIONAR",
                FechaSolicitud = now,
            });

            if (registro != null)
            {
                string movimientoTexto = $"|REGRESO DE PASEO {now:yyyy-MM-dd HH:mm} por operador {valetNombre}|";
                _context.ValetMovimientos.Add(new ValetMovimiento
                {
                    IdRegistro = registro.Id,
                    Reserva = registro.Reserva,
                    Servicio = "REGRESO",
                    FechaHora = now,
                    Operador = valetNombre,
                    MovimientoTexto = movimientoTexto
                });
                registro.Movimientos = (registro.Movimientos ?? "") + movimientoTexto;
            }

            await _context.SaveChangesAsync();

            return Ok(new { success = true, mensaje = "Regreso registrado. El coche vuelve a la cola de estacionar." });
        }

        // ============ FINALIZAR CON QR DE OPERADORA ================
        // El valet escanea el QR (tarjeta) de la operadora para confirmar la
        // entrega de la llave al estacionar o en una salida. Valida que el
        // código corresponda a un empleado real (padrón Pegasys), registra el
        // movimiento y cierra la solicitud (desaparece de la lista activa).
        [HttpPost]
        [Route("api/solicitudes/finalizar/{id}")]
        public async Task<IActionResult> FinalizarSolicitud(int id, string? codigoOperadora, string? numeroOperador)
        {
            var solicitud = await _context.ValetSolicitudes.FindAsync(id);
            if (solicitud == null)
                return NotFound(new { success = false });

            // ── Validar el código y resolver quién aprobó (nombre + código op) ──
            string? operadoraNombre = null;
            string? codigoOperadorAprobo = null;
            if (!string.IsNullOrWhiteSpace(codigoOperadora))
            {
                var codigo = codigoOperadora.Trim();

                // 1) Código propio (tabla CodigosLiberacion): trae el Nombre y el
                //    código de operador del usuario que lo creó — así en
                //    movimientos aparece que ESA persona autorizó.
                var ahora = DateTime.Now;
                var propio = await _context.CodigosLiberacion
                    .Where(c => c.Codigo == codigo && c.Activo
                        && (c.ExpiraEn == null || c.ExpiraEn > ahora))
                    .Select(c => new { c.Nombre, c.CodigoOperador })
                    .FirstOrDefaultAsync();
                if (propio != null)
                {
                    operadoraNombre = propio.Nombre;
                    codigoOperadorAprobo = propio.CodigoOperador;
                }

                // 2) Si no es propio, valida contra el padrón de empleados (gafete).
                if (string.IsNullOrWhiteSpace(operadoraNombre))
                {
                    try
                    {
                        operadoraNombre = await _pegasysContext.VV_TARJETAS_EMPLEADOS
                            .Where(e => e.clavenomina == codigo
                                     || e.ID_ICLASS == codigo
                                     || e.ID_MIFARE == codigo)
                            .Select(e => (e.c_mname + " " + e.c_lname).Trim())
                            .FirstOrDefaultAsync();
                        if (!string.IsNullOrWhiteSpace(operadoraNombre))
                            codigoOperadorAprobo = codigo;
                    }
                    catch
                    {
                        operadoraNombre = null;
                    }
                }
            }

            if (string.IsNullOrWhiteSpace(operadoraNombre))
            {
                return Ok(new
                {
                    success = false,
                    mensaje = "El código de operadora no es válido. Escanea el QR correcto."
                });
            }

            // Este endpoint SOLO valida el QR de la operadora y devuelve sus
            // datos. NO graba movimiento ni cierra la solicitud: el movimiento
            // "entregó llave" y el cierre ocurren al COMPLETAR la estacionada
            // (CerrarSolicitud, al guardar el cajón). Si el valet cancela, no
            // queda ni movimiento ni solicitud cerrada.
            string tipo = !string.IsNullOrWhiteSpace(solicitud.TipoSalida) ? solicitud.TipoSalida! : "VEHÍCULO";

            return Ok(new
            {
                success = true,
                operadora = operadoraNombre,
                codigoOperador = codigoOperadorAprobo,
                tipoSalida = tipo,
                folioVP = solicitud.FolioVP,
                mensaje = $"Operadora {operadoraNombre} validada. Completa la estacionada para registrar."
            });
        }

        // Cierra una solicitud (p. ej. la de ESTACIONAR una vez que SÍ se
        // completó el estacionamiento y se asignó el cajón). Al ponerse
        // "Finalizado" desaparece de la lista activa.
        [HttpPost]
        [Route("api/solicitudes/cerrar/{id}")]
        public async Task<IActionResult> CerrarSolicitud(int id, string? operadora, string? codigoOperador, string? numeroOperador)
        {
            var solicitud = await _context.ValetSolicitudes.FindAsync(id);
            if (solicitud == null)
                return NotFound(new { success = false });

            var now = DateTime.Now;
            var tipoSalida = (solicitud.TipoSalida ?? "").Trim().ToUpperInvariant();
            var requiereEntrega = tipoSalida == "PASEO" || tipoSalida == "PARCIAL"
                || tipoSalida == "SALIDA" || tipoSalida == "PERMANENTE";
            // Paseo/Salida ya fue estacionado en el area de entrega, pero aun
            // falta entregarlo al huesped. ESTACIONAR normal concluye aqui.
            solicitud.Estatus = requiereEntrega ? "Por entregar" : "Finalizado";
            solicitud.EstadoPaso = null;
            solicitud.FechaPendienteQr = null;
            solicitud.TiempoAtendido ??= now;

            // Aquí (al completarse la estacionada) SÍ se graba el movimiento de
            // entrega de llave, si al finalizar se validó una operadora.
            if (!string.IsNullOrWhiteSpace(operadora))
            {
                string valetNombre = numeroOperador ?? "Desconocido";
                if (!string.IsNullOrWhiteSpace(numeroOperador))
                {
                    try
                    {
                        var n = await _pegasysContext.VV_TARJETAS_EMPLEADOS
                            .Where(v => v.ID_ICLASS == numeroOperador)
                            .Select(v => v.c_mname + " " + v.c_lname)
                            .FirstOrDefaultAsync();
                        valetNombre = n?.Trim() ?? numeroOperador;
                    }
                    catch { valetNombre = numeroOperador; }
                }

                var registro = !string.IsNullOrWhiteSpace(solicitud.FolioVP)
                    ? await _context.ValetRegistros.FirstOrDefaultAsync(r => r.FolioVP == solicitud.FolioVP)
                    : null;

                if (registro != null)
                {
                    string aproboTxt = string.IsNullOrWhiteSpace(codigoOperador)
                        ? operadora
                        : $"{operadora} (op {codigoOperador})";
                    string movimientoTexto = $"|ENTREGA LLAVE A OPERADORA {aproboTxt} (QR) {now:yyyy-MM-dd HH:mm} por valet {valetNombre}|";
                    _context.ValetMovimientos.Add(new ValetMovimiento
                    {
                        IdRegistro = registro.Id,
                        Reserva = registro.Reserva,
                        Servicio = "ESTACIONAR",
                        FechaHora = now,
                        Operador = valetNombre,
                        MovimientoTexto = movimientoTexto
                    });
                    registro.Movimientos = (registro.Movimientos ?? "") + movimientoTexto;
                }
            }

            await _context.SaveChangesAsync();
            return Ok(new { success = true });
        }

        // El cajon ya fue guardado; desde este punto el valet no puede tomar
        // otra tarea hasta completar el QR o hasta que supervision intervenga.
        [HttpPost]
        [Route("api/solicitudes/pendienteQr/{id}")]
        public async Task<IActionResult> MarcarPendienteQr(int id, string? numeroOperador)
        {
            var solicitud = await _context.ValetSolicitudes.FindAsync(id);
            if (solicitud == null) return NotFound(new { success = false });
            if (!string.IsNullOrWhiteSpace(solicitud.TomadoPorId)
                && solicitud.TomadoPorId != numeroOperador)
                return Conflict(new { success = false, mensaje = "La solicitud pertenece a otro valet." });

            solicitud.EstadoPaso = "PENDIENTE_QR";
            solicitud.FechaPendienteQr = DateTime.Now;
            await _context.SaveChangesAsync();
            return Ok(new { success = true });
        }

        [HttpGet]
        [Route("api/supervision/solicitudes-pendientes")]
        public async Task<IActionResult> SolicitudesPendientesSupervision(string supervisorGafete, string? folio)
        {
            var supervisor = await ObtenerSupervisorAsync(supervisorGafete);
            if (supervisor == null) return Forbid();

            var query = _context.ValetSolicitudes.AsNoTracking()
                .Where(s => s.EstadoPaso == "PENDIENTE_QR");
            if (!string.IsNullOrWhiteSpace(folio))
                query = query.Where(s => s.FolioVP != null && s.FolioVP.Contains(folio.Trim()));

            var items = await query.OrderBy(s => s.FechaPendienteQr)
                .Select(s => new
                {
                    s.Id, s.FolioVP, s.NombreReserva, s.Habitacion, s.Resort,
                    s.TipoSalida, s.Estatus, s.EstadoPaso, s.FechaPendienteQr,
                    s.TomadoPor, s.TomadoPorId,
                    MinutosPendiente = s.FechaPendienteQr == null ? 0
                        : EF.Functions.DateDiffMinute(s.FechaPendienteQr, DateTime.Now)
                }).ToListAsync();
            return Ok(items);
        }

        public class AccionSupervisionDto
        {
            public string SupervisorGafete { get; set; } = "";
            public string Motivo { get; set; } = "";
            public string? NuevoValetGafete { get; set; }
        }

        [HttpPost]
        [Route("api/supervision/solicitudes/{id}/liberar")]
        public async Task<IActionResult> LiberarSolicitudSupervision(int id, [FromBody] AccionSupervisionDto dto)
            => await EjecutarAccionSupervision(id, dto, "LIBERAR");

        [HttpPost]
        [Route("api/supervision/solicitudes/{id}/cancelar")]
        public async Task<IActionResult> CancelarSolicitudSupervision(int id, [FromBody] AccionSupervisionDto dto)
            => await EjecutarAccionSupervision(id, dto, "CANCELAR");

        [HttpPost]
        [Route("api/supervision/solicitudes/{id}/reasignar")]
        public async Task<IActionResult> ReasignarSolicitudSupervision(int id, [FromBody] AccionSupervisionDto dto)
            => await EjecutarAccionSupervision(id, dto, "REASIGNAR");

        [HttpPost]
        [Route("api/supervision/solicitudes/{id}/discrepancia")]
        public async Task<IActionResult> RegistrarDiscrepanciaSupervision(int id, [FromBody] AccionSupervisionDto dto)
            => await EjecutarAccionSupervision(id, dto, "DISCREPANCIA");

        private async Task<IActionResult> EjecutarAccionSupervision(int id, AccionSupervisionDto dto, string accion)
        {
            var supervisor = await ObtenerSupervisorAsync(dto.SupervisorGafete);
            if (supervisor == null) return Forbid();
            if (string.IsNullOrWhiteSpace(dto.Motivo))
                return BadRequest(new { success = false, mensaje = "El motivo es obligatorio." });

            var solicitud = await _context.ValetSolicitudes.FindAsync(id);
            if (solicitud == null) return NotFound(new { success = false });
            var anterior = $"{solicitud.Estatus}|{solicitud.EstadoPaso}|{solicitud.TomadoPorId}|{solicitud.TomadoPor}";

            if (accion == "REASIGNAR")
            {
                if (string.IsNullOrWhiteSpace(dto.NuevoValetGafete))
                    return BadRequest(new { success = false, mensaje = "Indica el gafete del nuevo valet." });
                var nuevo = await _context.Usuarios.AsNoTracking()
                    .FirstOrDefaultAsync(u => u.Gafete == dto.NuevoValetGafete);
                if (nuevo == null)
                    return BadRequest(new { success = false, mensaje = "El nuevo valet no existe en Usuarios." });
                var ocupado = await _context.ValetSolicitudes.AsNoTracking().AnyAsync(s =>
                    s.Id != id && s.TomadoPorId == dto.NuevoValetGafete && s.EstadoPaso == "PENDIENTE_QR");
                if (ocupado)
                    return Conflict(new { success = false, mensaje = "El nuevo valet ya tiene un QR pendiente." });
                solicitud.TomadoPorId = nuevo.Gafete;
                solicitud.TomadoPor = nuevo.Nombre ?? nuevo.Username;
                // Se conserva PENDIENTE_QR: el nuevo valet debe completar el QR.
            }
            else if (accion == "LIBERAR")
            {
                solicitud.Estatus = null;
                solicitud.TiempoAtendido = null;
                solicitud.TomadoPor = null;
                solicitud.TomadoPorId = null;
                solicitud.FechaTomado = null;
                solicitud.EstadoPaso = null;
                solicitud.FechaPendienteQr = null;
            }
            else if (accion == "CANCELAR")
            {
                solicitud.Estatus = "Cancelada";
                solicitud.TiempoAtendido = DateTime.Now;
                solicitud.EstadoPaso = null;
                solicitud.FechaPendienteQr = null;
            }
            // DISCREPANCIA sólo deja evidencia: no cambia dueño, estatus ni bloqueo.

            var nuevoValor = $"{solicitud.Estatus}|{solicitud.EstadoPaso}|{solicitud.TomadoPorId}|{solicitud.TomadoPor}";
            _context.SolicitudesAuditoria.Add(new SolicitudAuditoria
            {
                SolicitudId = solicitud.Id,
                Accion = accion,
                ActorGafete = supervisor.Gafete,
                ActorNombre = supervisor.Nombre ?? supervisor.Username,
                Motivo = dto.Motivo.Trim(),
                ValorAnterior = anterior,
                ValorNuevo = nuevoValor
            });
            await _context.SaveChangesAsync();
            return Ok(new { success = true });
        }

        private async Task<Usuario?> ObtenerSupervisorAsync(string? gafete)
        {
            if (string.IsNullOrWhiteSpace(gafete)) return null;
            var usuario = await _context.Usuarios.AsNoTracking().FirstOrDefaultAsync(u => u.Gafete == gafete);
            if (usuario == null) return null;
            var roles = (usuario.Funciones ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            return roles.Contains("TI", StringComparer.OrdinalIgnoreCase)
                || roles.Contains("Administracion", StringComparer.OrdinalIgnoreCase) ? usuario : null;
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
