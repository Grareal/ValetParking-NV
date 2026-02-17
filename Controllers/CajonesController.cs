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
            var cajones = await _context.Cajones.OrderBy(c => c.Numero).ToListAsync();
            return Json(cajones);
        }

        [HttpPost]
        public async Task<IActionResult> OcupaCajon(int numero, string ubicacion)
        {
            var cajon = await _context.Cajones.FirstOrDefaultAsync(c => c.Numero == numero);
            if (cajon != null)
            {
                cajon.Ocupado = true;
               // cajon.Ubicacion = ubicacion;
                await _context.SaveChangesAsync();
                return Ok();
            }
            return NotFound();
        }

        [HttpPost]
        public async Task<IActionResult> LiberarCajon(int numero)
        {
            var cajon = await _context.Cajones
                .FirstOrDefaultAsync(c => c.Numero == numero);

            if (cajon != null)
            {
                cajon.Ocupado = false;

                // Opcional: limpiar ubicación si la usas
                // cajon.Ubicacion = null;

                await _context.SaveChangesAsync();
                return Ok();
            }

            return NotFound();
        }


    }
}
