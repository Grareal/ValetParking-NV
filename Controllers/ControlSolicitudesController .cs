using Microsoft.AspNetCore.Mvc;
using AppValetParking.Data;
using Microsoft.EntityFrameworkCore;
using AppValetParking.Models;

namespace AppValetParking.Controllers
{
    public class ControlSolicitudesController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ControlSolicitudesController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var solicitudes = await _context.ValetSolicitudes
                .OrderByDescending(s => s.FechaSolicitud)
                .ToListAsync();
            return View(solicitudes);
        }

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
    }
}
