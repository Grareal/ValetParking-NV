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
            private readonly PegasysDbContext _pegasysContext;
            private readonly IConfiguration _configuration;


            public BotonesController(ApplicationDbContext context, TcabdopeDbContext tcabdopeContext, PegasysDbContext pegasysContext, IConfiguration configuration)
            {
                _context = context;
                _tcabdopeContext = tcabdopeContext;
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



            [HttpPost]
            public async Task<IActionResult> Index(ValetRegistro registro)
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

                if (!string.IsNullOrWhiteSpace(registro.Reserva))
                {
                    var reserva = await _tcabdopeContext.ReservationAllView
                        .FirstOrDefaultAsync(r => r.CONFIRMATION_NO == registro.Reserva);

                    if (reserva != null)
                    {
                        registro.NombreReserva = reserva.GUEST_NAME;
                        registro.Habitacion = reserva.ROOM;
                        registro.Hotel = reserva.ROOM_CLASS;
                    }
                }

                var valetNombre = await _pegasysContext.VV_TARJETAS_EMPLEADOS
                    .Where(v => v.ID_ICLASS == registro.NumeroOperador)
                    .Select(v => v.c_mname + " " + v.c_lname)
                    .FirstOrDefaultAsync();

                registro.Valet = valetNombre?.Trim() ?? "Nombre no encontrado";

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

                // Limpiar solo el número del operador para que el usuario lo ingrese
                registro.NumeroOperador = "";
                 registro.CajonBuffer = "";   // <--- Limpiar aquí


            ViewBag.EnfocarNumero = true; // bandera para JS

                return View("EditarRegistro", registro);
            }

            [HttpPost]
            public async Task<IActionResult> EditarRegistro(ValetRegistro model)
            {
                if (string.IsNullOrWhiteSpace(model.FolioVP) ||
                    string.IsNullOrWhiteSpace(model.CajonBuffer) ||
                    string.IsNullOrWhiteSpace(model.NumeroOperador))
                {
                    ViewBag.Mensaje = "Folio, Cajón Buffer y Número del operador son obligatorios.";
                    return View("EditarRegistro", model);
                }

                var registro = await _context.ValetRegistros.FindAsync(model.Id);

                if (registro == null)
                {
                    ViewBag.Mensaje = "Registro no encontrado para actualizar.";
                    return View("EditarRegistro", model);
                }

                registro.Servicio = model.Servicio;
                registro.CajonBuffer = model.CajonBuffer;
                registro.NumeroOperador = model.NumeroOperador;
                registro.HoraSalida = DateTime.Now.TimeOfDay;


                var valetNombre = await _pegasysContext.VV_TARJETAS_EMPLEADOS
                    .Where(v => v.ID_ICLASS == model.NumeroOperador)
                    .Select(v => v.c_mname + " " + v.c_lname)
                    .FirstOrDefaultAsync();

                registro.Valet = valetNombre?.Trim() ?? "Nombre no encontrado";

                Console.WriteLine("VALOR DE VALET RECIBIDO: " + model.Valet);
                var now = DateTime.Now;

                string nuevoMovimiento = $"|{registro.Servicio} {DateTime.Now:yyyy-MM-dd HH:mm} por operador {registro.Valet}|";
                registro.Movimientos = (registro.Movimientos ?? "") + nuevoMovimiento;

          

                await _context.SaveChangesAsync();  // Guarda los cambios

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

                // Guardamos tanto la edición del registro como el nuevo movimiento
                await _context.SaveChangesAsync();

                // Cargar nuevamente el registro actualizado para mostrar en la vista
                var registroActualizado = await _context.ValetRegistros.FindAsync(model.Id);

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
                if (string.IsNullOrWhiteSpace(confirmacion))
                    return BadRequest("Confirmación vacía");

                var reserva = await _tcabdopeContext.ReservationAllView
                    .FirstOrDefaultAsync(r => r.CONFIRMATION_NO == confirmacion);

                if (reserva == null)
                    return NotFound("Reserva no encontrada");

                return Json(new
                {
                    habitacion = reserva.ROOM,
                    hotel = reserva.ROOM_CLASS,
                    nombre = reserva.GUEST_NAME
                });
            }
        }
    }
