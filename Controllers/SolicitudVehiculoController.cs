using Microsoft.AspNetCore.Mvc;

namespace AppValetParking.Controllers
{
    public class SolicitudVehiculoController : Controller
    {
        // Abre la vista
        [HttpGet]
        public IActionResult Index()
        {
            return View();
        }
    }
}
