using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AppValetParking.Data;
using AppValetParking.Models;

namespace AppValetParking.Controllers
{
    public class CajonesController : Controller
    {
        private readonly ApplicationDbContext _context;
        public CajonesController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> GetCajones()
        {
            var cajones = await _context.Cajones.ToListAsync();
            var ordenados = cajones
                .OrderBy(c => c.Ubicacion)
                .ThenBy(c => int.TryParse(c.Numero, out var n) ? n : int.MaxValue)
                .ToList();
            return Json(ordenados);
        }

        // numero + ubicacion identifican un cajón de forma única: el mismo
        // número puede repetirse en distintas zonas (ej. Buffer 12 y Remoto A-12).
        [HttpPost]
        public async Task<IActionResult> OcupaCajon(string numero, string ubicacion)
        {
            var cajon = await _context.Cajones
                .FirstOrDefaultAsync(c => c.Numero == numero && c.Ubicacion == ubicacion);
            if (cajon != null)
            {
                cajon.Ocupado = true;
                await _context.SaveChangesAsync();
                return Ok();
            }
            return NotFound();
        }

        [HttpPost]
        public async Task<IActionResult> LiberarCajon(string numero, string ubicacion)
        {
            var cajon = await _context.Cajones
                .FirstOrDefaultAsync(c => c.Numero == numero && c.Ubicacion == ubicacion);

            if (cajon != null)
            {
                cajon.Ocupado = false;
                await _context.SaveChangesAsync();
                return Ok();
            }

            return NotFound();
        }


    }
}
