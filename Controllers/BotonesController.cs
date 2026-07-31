    using Microsoft.AspNetCore.Mvc;
    using AppValetParking.Data;
    using AppValetParking.Models;
    using System.Threading.Tasks;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Configuration;
    using System.Text.Json;

    using System;
    using System.Linq;

    namespace AppValetParking.Controllers
    {
        public class BotonesController : Controller
        {
        private readonly ApplicationDbContext _context;
        private readonly TcabdopeDbContext _tcabdopeContext;
        private readonly TcabdopeNewDbContext _tcabdopeNewContext;
        private readonly PegasysDbContext _pegasysContext;
        private readonly IConfiguration _configuration;


        public BotonesController(
            ApplicationDbContext context,
            TcabdopeDbContext tcabdopeContext,
            TcabdopeNewDbContext tcabdopeNewContext,
            PegasysDbContext pegasysContext,
            IConfiguration configuration)
        {
            _context = context;
            _tcabdopeContext = tcabdopeContext;
            _tcabdopeNewContext = tcabdopeNewContext; 
            _pegasysContext = pegasysContext;
            _configuration = configuration;
        }

        [HttpGet]
            public IActionResult Index()
            {
                var userAgent = Request.Headers["User-Agent"].ToString().ToLower();
                bool esMovil = userAgent.Contains("iphone") || userAgent.Contains("android") || userAgent.Contains("mobile");

                bool modoRafaga = _configuration.GetValue<bool>("modoRafaga");
                int cantidadDisparos = _configuration.GetValue<int>("cantidadDisparos");
                var identificadores = _configuration.GetSection("identificadores")
                    .GetChildren()
                    .ToDictionary(x => x.Key, x => x.Value);

                ViewBag.ModoRafaga = modoRafaga;
                ViewBag.CantidadDisparos = cantidadDisparos;
                ViewBag.Identificadores = JsonSerializer.Serialize(identificadores);

                 var modelo = new ValetRegistro();

                return View(esMovil ? "BotonesViewMobile" : "BotonesView", modelo);
            }

        [HttpGet]
        public async Task<IActionResult> ObtenerRegistro(string folio)
        {
            if (string.IsNullOrWhiteSpace(folio))
                return BadRequest(new { error = "Folio vacío" });

            var registro = await _context.ValetRegistros
                .FirstOrDefaultAsync(r => r.FolioVP == folio);

            // Fallback: si el folio fue transferido a uno nuevo, resolverlo para
            // que la etiqueta vieja siga encontrando el vehículo.
            if (registro == null)
            {
                var nuevo = await _context.FoliosTransferidos
                    .Where(t => t.FolioAnterior == folio)
                    .OrderByDescending(t => t.Fecha)
                    .Select(t => t.FolioNuevo)
                    .FirstOrDefaultAsync();
                if (nuevo != null)
                    registro = await _context.ValetRegistros
                        .FirstOrDefaultAsync(r => r.FolioVP == nuevo);
            }

            if (registro == null)
                return NotFound(new { error = "No encontrado" });

            return Json(new
            {
                id = registro.Id,
                folioVP = registro.FolioVP,
                reserva = registro.Reserva,
                hotel = registro.Hotel,
                habitacion = registro.Habitacion,
                servicio = registro.Servicio,
                cajonBuffer = registro.CajonBuffer,
                situacion = registro.Situacion,
                valet = registro.Valet,
                hostname = registro.HOSTNAME
            });
        }


        [HttpPost]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> Index(ValetRegistro registro, int? solicitudReservacionId = null)
            {
                bool esMovil = Request.Headers["User-Agent"].ToString().ToLower().Contains("iphone") ||
                               Request.Headers["User-Agent"].ToString().ToLower().Contains("android") ||
                               Request.Headers["User-Agent"].ToString().ToLower().Contains("android") ||
                               Request.Headers["User-Agent"].ToString().ToLower().Contains("mobile");

                string vista = esMovil ? "BotonesViewMobile" : "BotonesView";

                if (string.IsNullOrWhiteSpace(registro.FolioVP) ||
                    string.IsNullOrWhiteSpace(registro.NumeroOperador))
                {
                    ViewBag.Mensaje = "Folio y Número del operador son obligatorios.";
                    return View(vista, registro);
                }
            if (string.IsNullOrWhiteSpace(registro.NumeroOperador) || registro.NumeroOperador.Trim() == "0")
            {
                ViewBag.Mensaje = "El campo Número no puede estar vacío ni ser 0.";
                return View(vista, registro);
            }

            if (string.IsNullOrWhiteSpace(registro.NumeroOperador) || registro.NumeroOperador == "0000")
            {
                ModelState.AddModelError("NumeroOperador", "Debes ingresar un número válido.");
                return View(vista);
            }

            bool folioExiste = await _context.ValetRegistros
                    .AnyAsync(r => r.FolioVP == registro.FolioVP);

                if (folioExiste)
                {
                    ViewBag.Mensaje = $"El folio '{registro.FolioVP}' ya existe. Por favor, utiliza otro.";
                    return View(vista, registro);
                }

                var now = DateTime.Now;
                registro.Fecha = now;

                registro.Solicitud = now.TimeOfDay;
                registro.HoraSalida = new TimeSpan(now.Hour, now.Minute, now.Second);
            registro.Situacion = "USADO";

            //USO DE CONFIRMATION O EXTERNAL
            if (!string.IsNullOrWhiteSpace(registro.Reserva))
            {
                try
                {
                    registro.Reserva = registro.Reserva.Trim();

                    var reserva = await _tcabdopeContext.ReservationAllView
                        .FirstOrDefaultAsync(r => r.CONFIRMATION_NO == registro.Reserva);

                    if (reserva == null && double.TryParse(registro.Reserva, out double external))
                    {
                        reserva = await _tcabdopeContext.ReservationAllView
                            .FirstOrDefaultAsync(r =>
                                r.RESV_NAME_ID.HasValue &&
                                Math.Abs(r.RESV_NAME_ID.Value - external) < 1);
                    }

                    if (reserva != null)
                    {
                        registro.Reserva = reserva.CONFIRMATION_NO;
                        registro.NombreReserva = reserva.GUEST_NAME;
                        registro.Habitacion = reserva.ROOM;
                        registro.Hotel = reserva.ROOM_CLASS;
                    }
                    else
                    {
                        var reservaAlt = await _tcabdopeNewContext.ReservationSearch
                            .Where(x =>
                                x.RESORT == "VINV" &&
                                (x.CONFIRMATION_NO == registro.Reserva ||
                                 x.EXTERNAL_REFERENCE == registro.Reserva))
                            .Select(x => new
                            {
                                confirmation = x.CONFIRMATION_NO,
                                nombre = (x.SGUEST_FIRSTNAME ?? "") + " " + (x.SGUEST_NAME ?? ""),
                                habitacion = x.ROOM,
                                hotel = x.ROOM_CLASS
                            })
                            .FirstOrDefaultAsync();

                        if (reservaAlt != null)
                        {
                            registro.Reserva = reservaAlt.confirmation;
                            registro.NombreReserva = reservaAlt.nombre;
                            registro.Habitacion = reservaAlt.habitacion;
                            registro.Hotel = reservaAlt.hotel;
                        }
                    }
                }
                catch
                {
                    // No detener el registro si falla la consulta
                }
            }

            string valetNombre = "Sin conexión Pegasys";
            try
            {
                valetNombre = await _pegasysContext.VV_TARJETAS_EMPLEADOS
                    .Where(v => v.ID_ICLASS == registro.NumeroOperador)
                    .Select(v => v.c_mname + " " + v.c_lname)
                    .FirstOrDefaultAsync() ?? "Nombre no encontrado";

                valetNombre = valetNombre.Trim();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ Pegasys no disponible: {ex.Message}");
                // Continúa con el valor por defecto, no rompe el registro
            }

            registro.Valet = valetNombre;

            string nuevoMovimiento = $"|{registro.Servicio} {now:yyyy-MM-dd HH:mm} por operador {registro.Valet}|";
                registro.Movimientos = (registro.Movimientos ?? "") + nuevoMovimiento;

                _context.ValetRegistros.Add(registro);
                await _context.SaveChangesAsync();  // registro.Id generado

                var movimiento = new ValetMovimiento
                {
                    IdRegistro = registro.Id,
                    Reserva = registro.Reserva,
                    Servicio = registro.Servicio,
                    FechaHora = now,
                    Operador = registro.Valet,
                    MovimientoTexto = nuevoMovimiento
                };

                _context.ValetMovimientos.Add(movimiento);
                await _context.SaveChangesAsync();  // movimiento.Id generado

                //  asignamos Operacion con el Id del movimiento
                registro.Operacion = movimiento.Id.ToString();
                await _context.SaveChangesAsync();  // Guardar actualización en registro

                // Cierra la solicitud RESERVADO (RESERVACION/REGISTRAR) de esta
                // reservación: el coche pasó de RESERVADO a REGISTRADO, así que
                // esa tarea ya se cumplió y debe desaparecer de la cola.
                // ROBUSTO: se cierra por el Id de la solicitud que se tomó (viene
                // desde la app). El folio como respaldo (compat con flujos viejos),
                // porque el folio del RESERVADO suele ser el número de reserva y no
                // el folio VP del registro.
                ValetSolicitud reservado = null;
                if (solicitudReservacionId.HasValue && solicitudReservacionId.Value > 0)
                {
                    reservado = await _context.ValetSolicitudes.FirstOrDefaultAsync(s =>
                        s.Id == solicitudReservacionId.Value
                        && (s.TipoSalida == "RESERVACION" || s.TipoSalida == "REGISTRAR")
                        && s.Estatus != "Finalizado" && s.Estatus != "Entregado");
                }
                if (reservado == null && !string.IsNullOrWhiteSpace(registro.FolioVP))
                {
                    reservado = await _context.ValetSolicitudes.FirstOrDefaultAsync(s =>
                        s.FolioVP == registro.FolioVP
                        && (s.TipoSalida == "RESERVACION" || s.TipoSalida == "REGISTRAR")
                        && s.Estatus != "Finalizado" && s.Estatus != "Entregado");
                }
                if (reservado != null)
                {
                    reservado.Estatus = "Finalizado";
                    reservado.TiempoAtendido ??= now;
                }

                // Crear solicitud "ESTACIONAR" para que el vehículo recién
                // registrado aparezca en la cola de Solicitudes.
                _context.ValetSolicitudes.Add(new ValetSolicitud
                {
                    FolioVP = registro.FolioVP,
                    NombreReserva = registro.NombreReserva,
                    Habitacion = registro.Habitacion,
                    Resort = registro.Hotel,
                    TipoSalida = "ESTACIONAR",
                    FechaSolicitud = now,
                });
                await _context.SaveChangesAsync();

                ViewBag.Mensaje = "Registro guardado correctamente.";
                bool modoRafaga = _configuration.GetValue<bool>("modoRafaga");
                int cantidadDisparos = _configuration.GetValue<int>("cantidadDisparos");
                var identificadores = _configuration.GetSection("identificadores")
                    .GetChildren()
                    .ToDictionary(x => x.Key, x => x.Value);

                ViewBag.ModoRafaga = modoRafaga;
                ViewBag.CantidadDisparos = cantidadDisparos;
                ViewBag.Identificadores = JsonSerializer.Serialize(identificadores);
                var modeloVacio = new ValetRegistro();

                return View(vista, modeloVacio);
            }


            [HttpGet]
            public async Task<IActionResult> VerificarFolio(string folio)
            {
                if (string.IsNullOrWhiteSpace(folio))
                    return BadRequest("Folio vacío");

                bool existe = await _context.ValetRegistros.AnyAsync(r => r.FolioVP == folio);
                // También cuenta como existente si el folio fue transferido a otro.
                if (!existe)
                    existe = await _context.FoliosTransferidos.AnyAsync(t => t.FolioAnterior == folio);
                return Json(new { existe });
            }

            [HttpGet]
            public async Task<IActionResult> EditarPorFolio(string folio)
            {
                if (string.IsNullOrWhiteSpace(folio))
                {
                    ViewBag.Mensaje = "Por favor coloca un folio para buscar.";
                    return View("EditarRegistro", new ValetRegistro());
                }

                var registro = await _context.ValetRegistros
                    .FirstOrDefaultAsync(r => r.FolioVP == folio);

                if (registro == null)
                {
                    ViewBag.Mensaje = "No se encontró ningún registro con folio {folio}. ";
                    return View("EditarRegistro", new ValetRegistro());  // <-- Aquí un modelo vacío

                }

            ViewBag.CajonActual = registro.CajonBuffer; // <-- el valor que quieres mostrar


            // Limpiar solo el número del operador para que el usuario lo ingrese
            registro.NumeroOperador = "";
 
            registro.CajonBuffer = "";   // <--- Limpiar aquí


            ViewBag.EnfocarNumero = true; // bandera para JS

                return View("EditarRegistro", registro);
            }

        [HttpPost]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> EditarRegistro(ValetRegistro model)
        {
            if (string.IsNullOrWhiteSpace(model.FolioVP) ||
                string.IsNullOrWhiteSpace(model.CajonBuffer) ||
                string.IsNullOrWhiteSpace(model.NumeroOperador))
            {
                ViewBag.Mensaje = "Folio, Cajón Buffer y Número del operador son obligatorios.";
                return View("EditarRegistro", model);
            }

            // ── Buscar por FolioVP si no viene Id ────────────────
            ValetRegistro registro = null;

            if (model.Id > 0)
                registro = await _context.ValetRegistros.FindAsync(model.Id);

            if (registro == null && !string.IsNullOrWhiteSpace(model.FolioVP))
                registro = await _context.ValetRegistros
                    .FirstOrDefaultAsync(r => r.FolioVP == model.FolioVP);

            if (registro == null)
            {
                ViewBag.Mensaje = "Registro no encontrado para actualizar.";
                return View("EditarRegistro", model);
            }

            registro.Servicio = model.Servicio;
            registro.CajonBuffer = model.CajonBuffer;
            registro.NumeroOperador = model.NumeroOperador;
            registro.HoraSalida = DateTime.Now.TimeOfDay;

            if (model.Servicio?.ToUpper() == "SALIDA")
                registro.Situacion = "SALIDA";

            string valetNombre = "Sin conexión Pegasys";
            try
            {
                var nombre = await _pegasysContext.VV_TARJETAS_EMPLEADOS
                    .Where(v => v.ID_ICLASS == model.NumeroOperador)
                    .Select(v => v.c_mname + " " + v.c_lname)
                    .FirstOrDefaultAsync();
                valetNombre = nombre?.Trim() ?? "Nombre no encontrado";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ Pegasys no disponible: {ex.Message}");
            }

            registro.Valet = valetNombre;

            var now = DateTime.Now;
            string nuevoMovimiento = $"|{registro.Servicio} {now:yyyy-MM-dd HH:mm} por operador {registro.Valet}|";
            registro.Movimientos = (registro.Movimientos ?? "") + nuevoMovimiento;

            await _context.SaveChangesAsync();

            var movimiento = new ValetMovimiento
            {
                IdRegistro = registro.Id,
                Reserva = registro.Reserva,
                Servicio = registro.Servicio,
                FechaHora = now,
                Operador = registro.Valet,
                MovimientoTexto = nuevoMovimiento
            };

            _context.ValetMovimientos.Add(movimiento);
            await _context.SaveChangesAsync();

            var registroActualizado = await _context.ValetRegistros.FindAsync(registro.Id);
            ViewBag.Mensaje = "Servicio actualizado correctamente.";
            registroActualizado.NumeroOperador = "";
            registroActualizado.CajonBuffer = "";
            registroActualizado.Valet = "";
            registroActualizado.Hotel = "";
            registroActualizado.FolioVP = "";

            return View(registroActualizado);
        }

        [HttpGet]
            public IActionResult EditarRegistro()
            {
                ViewBag.Mensaje = TempData["Mensaje"];
                var modeloVacio = new ValetRegistro();  
                return View(modeloVacio);
            }



        [HttpGet]
        public async Task<IActionResult> ObtenerReserva(string confirmacion)
        {
            try
            {
                Console.WriteLine("===== DEBUG OBTENER RESERVA =====");
                Console.WriteLine($"Valor recibido RAW: '{confirmacion}'");

                confirmacion = confirmacion?.Trim();

                Console.WriteLine($"Valor TRIM: '{confirmacion}'");
                Console.WriteLine($"Length: {confirmacion?.Length}");

                if (string.IsNullOrWhiteSpace(confirmacion))
                {
                    Console.WriteLine("❌ confirmacion vacía");
                    return BadRequest(new { error = "Confirmación vacía" });
                }

                ReservationAllView reserva = null;

                // ==============================
                // 1. BUSCAR POR RESERVA O TSW
                // ==============================
                // El huésped llega indistintamente con el número de reserva
                // (CONFIRMATION_NO) o con el TSW (EXTERNAL_REFERENCE), así que
                // se buscan los dos de una vez y por igualdad.
                reserva = await _tcabdopeContext.ReservationAllView
                    .FirstOrDefaultAsync(r => r.CONFIRMATION_NO == confirmacion ||
                                              r.EXTERNAL_REFERENCE == confirmacion);

                Console.WriteLine(reserva == null
                    ? "❌ No encontrado por CONFIRMATION_NO / EXTERNAL_REFERENCE"
                    : "✅ Encontrado por CONFIRMATION_NO / EXTERNAL_REFERENCE");

                // ==============================
                // 2. BUSCAR POR RESV_NAME_ID
                // ==============================
                if (reserva == null && double.TryParse(confirmacion, out double resvId))
                {
                    Console.WriteLine($"Intentando por RESV_NAME_ID: {resvId}");

                    reserva = await _tcabdopeContext.ReservationAllView
                        .FirstOrDefaultAsync(r => r.RESV_NAME_ID.HasValue &&
                                                  Math.Abs(r.RESV_NAME_ID.Value - resvId) < 1);

                    Console.WriteLine(reserva == null
                        ? "❌ No encontrado por RESV_NAME_ID"
                        : "✅ Encontrado por RESV_NAME_ID");
                }

                // ==============================
                // 3. FALLBACK
                // ==============================
                if (reserva == null)
                {
                    Console.WriteLine("🔍 Buscando en ReservationSearch...");

                    var reservaAlt = await _tcabdopeNewContext.ReservationSearch
                        .Where(x => x.RESORT == "VINV" &&
                                   (x.CONFIRMATION_NO == confirmacion || x.EXTERNAL_REFERENCE == confirmacion))
                        .Select(x => new
                        {
                            confirmation = x.CONFIRMATION_NO,
                            nombre = (x.SGUEST_FIRSTNAME ?? "") + " " + (x.SGUEST_NAME ?? ""),
                            habitacion = x.ROOM,
                            hotel = x.ROOM_CLASS
                        })
                        .FirstOrDefaultAsync();

                    if (reservaAlt != null)
                    {
                        Console.WriteLine("✅ Encontrado en ReservationSearch");
                        return Json(reservaAlt);
                    }

                    Console.WriteLine("❌ Tampoco encontrado en ReservationSearch");
                }

                if (reserva == null)
                {
                    Console.WriteLine("🚨 RESULTADO FINAL: NO ENCONTRADO");
                    return NotFound(new { error = "Reserva no encontrada" });
                }

                Console.WriteLine("🎯 RESULTADO FINAL: ENCONTRADO");
                Console.WriteLine($"CONFIRMATION_NO: '{reserva.CONFIRMATION_NO}'");
                Console.WriteLine($"GUEST: {reserva.GUEST_NAME}");

                return Json(new
                {
                    habitacion = reserva.ROOM,
                    hotel = reserva.ROOM_CLASS,
                    nombre = reserva.GUEST_NAME,
                    confirmation = reserva.CONFIRMATION_NO,
                    tsw = reserva.EXTERNAL_REFERENCE
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"🔥 ERROR: {ex.Message}");
                return StatusCode(500, new { error = ex.Message });
            }
        }

    }
    }
