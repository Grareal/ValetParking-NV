using Microsoft.AspNetCore.Mvc;
using AppValetParking.Models;
using System.Collections.Generic;
using AppValetParking.Services;

namespace AppValetParking.Controllers
{
    public class DashboardController : Controller
    {
        private readonly AccesoVistasService _accesos;

        public DashboardController(AccesoVistasService accesos) => _accesos = accesos;

        public async Task<IActionResult> Index(bool accesoDenegado = false)
        {
            if (HttpContext.Session.GetString("Usuario") == null)
                return RedirectToAction("Login", "Account");

            var permisos = HttpContext.Session.GetString("Permisos") ?? "";
            ViewBag.Permisos = permisos;

            ViewBag.Modulos = await _accesos.ObtenerMenuAsync(permisos);
            ViewBag.AccesoDenegado = accesoDenegado;

            return View();
        }
    }
}
