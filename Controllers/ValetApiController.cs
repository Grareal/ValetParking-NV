using Microsoft.AspNetCore.Mvc;
using AppValetParking.Data;
using AppValetParking.Models;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;

namespace AppValetParking.Controllers
{
    [Route("api/valet")]
    [ApiController]
    public class ValetApiController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public ValetApiController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET api/valet/folio?folio=VP123
        [HttpGet("folio")]
        public async Task<IActionResult> ObtenerDatosFolio(string folio)
        {
            if (string.IsNullOrWhiteSpace(folio))
                return BadRequest(new { mensaje = "Folio vac�o" });

            var registro = await _context.ValetRegistros
                .FirstOrDefaultAsync(r => r.FolioVP == folio);

            if (registro == null)
                return NotFound(new { mensaje = "Folio no encontrado" });

            return Ok(new
            {
                registro.NombreReserva,
                registro.Habitacion,
                registro.Hotel,
                registro.Servicio
            });
        }

        // POST api/valet/crear
        [HttpPost("crear")]
        public async Task<IActionResult> CrearSolicitud([FromBody] SolicitudVehiculoDto solicitud)
        {
            if (solicitud == null || string.IsNullOrWhiteSpace(solicitud.FolioVP))
                return BadRequest(new { exito = false, mensaje = "Folio vac�o" });

            var registro = await _context.ValetRegistros
                .FirstOrDefaultAsync(r => r.FolioVP == solicitud.FolioVP);

            if (registro == null)
                return NotFound(new { exito = false, mensaje = "Folio no encontrado" });

            var nuevaSolicitud = new ValetSolicitud
            {
                FolioVP = registro.FolioVP,
                Destino = solicitud.Destino,
                Resort = solicitud.Resort ?? registro.Hotel,
                Habitacion = registro.Habitacion,
                NombreReserva = registro.NombreReserva ?? ".",
                NombreSolicitante = solicitud.NombreSolicitante,
                ApellidoSolicitante = solicitud.ApellidoSolicitante,
                Telefono = solicitud.Telefono,
                Correo = solicitud.Correo,
                MarcaVehiculo = solicitud.MarcaVehiculo,
                ColorVehiculo = solicitud.ColorVehiculo,
                TipoSalida = solicitud.TipoSalida,
                Comentarios = solicitud.Comentarios
            };

            _context.ValetSolicitudes.Add(nuevaSolicitud);

            // NOTA: el estatus del vehículo NO se cambia aquí. Pedir una salida
            // solo crea la tarea; el estatus cambia cuando el valet confirma la
            // entrega (click) en api/solicitudes/entregar/{id}. Así el estado
            // refleja la acción real, no la solicitud.
            await _context.SaveChangesAsync();

            return Ok(new { exito = true, mensaje = $"Solicitud enviada para el folio {solicitud.FolioVP}" });
        }


        public class SolicitudVehiculoDto
        {
            public string FolioVP { get; set; } = string.Empty;
            public string? Destino { get; set; }
            public string? Resort { get; set; }
            public string? NombreSolicitante { get; set; }
            public string? ApellidoSolicitante { get; set; }
            public string? Telefono { get; set; }
            public string? Correo { get; set; }
            public string? MarcaVehiculo { get; set; }
            public string? ColorVehiculo { get; set; }
            public string? TipoSalida { get; set; }
            public string? Comentarios { get; set; }
        }
    }
}
