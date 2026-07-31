using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AppValetParking.Data;
using AppValetParking.Models;

namespace AppValetParking.Controllers
{
	[ApiController]
	[Route("api/[controller]")]
	public class VehiculosController : ControllerBase
	{
		private readonly ApplicationDbContext _context;

		public VehiculosController(ApplicationDbContext context)
		{
			_context = context;
		}

        [HttpPost("Guardar")]
        public async Task<IActionResult> GuardarVehiculo([FromBody] VehiculoInfo vehiculo)
        {
            try
            {
                var existente = await _context.VehiculosInfo
                    .FirstOrDefaultAsync(v => v.FolioVP == vehiculo.FolioVP);

                if (existente != null)
                {
                    existente.Placas = vehiculo.Placas;
                    existente.Marca = vehiculo.Marca;
                    existente.Modelo = vehiculo.Modelo;
                    existente.Color = vehiculo.Color;

                    await _context.SaveChangesAsync();

                    return Ok(new
                    {
                        success = true,
                        mensaje = "Veh�culo actualizado correctamente",
                        id = existente.Id
                    });
                }

                vehiculo.FechaRegistro = DateTime.Now;
                vehiculo.Estatus = "Dentro";

                _context.VehiculosInfo.Add(vehiculo);

                await _context.SaveChangesAsync();

                return Ok(new
                {
                    success = true,
                    mensaje = "Veh�culo guardado correctamente",
                    id = vehiculo.Id
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new
                {
                    success = false,
                    mensaje = ex.ToString()
                });
            }
        }

        // Devuelve los colores ya capturados (DISTINCT sobre VehiculosInfo) para
        // alimentar el autocompletado del wizard. No requiere tabla nueva: cuando
        // el usuario escribe un color inexistente y guarda el vehículo, el color
        // queda en VehiculosInfo y aparece aquí la próxima vez.
        [HttpGet("Colores")]
        public async Task<IActionResult> ObtenerColores(string? q = null)
        {
            var colores = await _context.VehiculosInfo
                .Where(v => v.Color != null && v.Color != "")
                .Select(v => v.Color!)
                .Distinct()
                .ToListAsync();

            IEnumerable<string> resultado = colores;
            if (!string.IsNullOrWhiteSpace(q))
            {
                var qn = q.Trim().ToLowerInvariant();
                resultado = resultado.Where(c => c.ToLowerInvariant().Contains(qn));
            }

            resultado = resultado
                .OrderBy(c => c, StringComparer.OrdinalIgnoreCase)
                .Take(50);

            return Ok(resultado.ToList());
        }

        [HttpGet("PorFolio/{folio}")]
		public async Task<IActionResult> ObtenerPorFolio(string folio)
		{
			var vehiculo = await _context.VehiculosInfo
				.FirstOrDefaultAsync(x => x.FolioVP == folio);

			// Fallback: folio transferido → resolver al folio nuevo.
			if (vehiculo == null)
			{
				var nuevo = await _context.FoliosTransferidos
					.Where(t => t.FolioAnterior == folio)
					.OrderByDescending(t => t.Fecha)
					.Select(t => t.FolioNuevo)
					.FirstOrDefaultAsync();
				if (nuevo != null)
					vehiculo = await _context.VehiculosInfo
						.FirstOrDefaultAsync(x => x.FolioVP == nuevo);
			}

			if (vehiculo == null)
				return NotFound();

			return Ok(vehiculo);
		}

        [HttpPost("SubirFoto")]
        [RequestSizeLimit(10_000_000)]
        public async Task<IActionResult> SubirFoto([FromForm] string folioVP, [FromForm] string slot, [FromForm] IFormFile archivo, [FromForm] string? comentario = null)
        {
            if (string.IsNullOrWhiteSpace(folioVP) || string.IsNullOrWhiteSpace(slot) || archivo == null || archivo.Length == 0)
                return BadRequest(new { success = false, mensaje = "folioVP, slot y archivo son obligatorios" });

            try
            {
                var carpeta = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", folioVP);
                Directory.CreateDirectory(carpeta);

                var extension = Path.GetExtension(archivo.FileName);
                if (string.IsNullOrWhiteSpace(extension)) extension = ".jpg";
                var nombreArchivo = $"{slot}{extension}";
                var rutaFisica = Path.Combine(carpeta, nombreArchivo);

                using (var stream = new FileStream(rutaFisica, FileMode.Create))
                {
                    await archivo.CopyToAsync(stream);
                }

                var rutaRelativa = $"/uploads/{folioVP}/{nombreArchivo}";

                var existente = await _context.VehiculoFotos
                    .FirstOrDefaultAsync(f => f.FolioVP == folioVP && f.Slot == slot);

                if (existente != null)
                {
                    existente.RutaArchivo = rutaRelativa;
                    existente.FechaCreacion = DateTime.Now;
                    if (comentario != null) existente.Comentario = comentario;
                }
                else
                {
                    _context.VehiculoFotos.Add(new VehiculoFoto
                    {
                        FolioVP = folioVP,
                        Slot = slot,
                        RutaArchivo = rutaRelativa,
                        Comentario = comentario
                    });
                }

                await _context.SaveChangesAsync();

                return Ok(new { success = true, url = rutaRelativa });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, mensaje = ex.ToString() });
            }
        }

        [HttpGet("Fotos/{folio}")]
        public async Task<IActionResult> ObtenerFotos(string folio)
        {
            var fotos = await _context.VehiculoFotos
                .Where(f => f.FolioVP == folio)
                .ToListAsync();

            // Fallback: folio transferido → traer las fotos del folio nuevo.
            if (fotos.Count == 0)
            {
                var nuevo = await _context.FoliosTransferidos
                    .Where(t => t.FolioAnterior == folio)
                    .OrderByDescending(t => t.Fecha)
                    .Select(t => t.FolioNuevo)
                    .FirstOrDefaultAsync();
                if (nuevo != null)
                    fotos = await _context.VehiculoFotos
                        .Where(f => f.FolioVP == nuevo)
                        .ToListAsync();
            }

            return Ok(fotos);
        }

        // Actualiza (o limpia) el comentario de una foto ya subida.
        [HttpPost("ComentarioFoto")]
        public async Task<IActionResult> ComentarioFoto([FromForm] string folioVP, [FromForm] string slot, [FromForm] string? comentario)
        {
            if (string.IsNullOrWhiteSpace(folioVP) || string.IsNullOrWhiteSpace(slot))
                return BadRequest(new { success = false, mensaje = "folioVP y slot son obligatorios" });

            var foto = await _context.VehiculoFotos
                .FirstOrDefaultAsync(f => f.FolioVP == folioVP && f.Slot == slot);

            if (foto == null)
                return NotFound(new { success = false, mensaje = "Foto no encontrada" });

            foto.Comentario = string.IsNullOrWhiteSpace(comentario) ? null : comentario.Trim();
            await _context.SaveChangesAsync();

            return Ok(new { success = true });
        }
	}
}