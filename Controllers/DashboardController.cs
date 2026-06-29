using Microsoft.AspNetCore.Mvc;
using AppValetParking.Models;
using System.Collections.Generic;

namespace AppValetParking.Controllers
{
    public class DashboardController : Controller
    {
        public IActionResult Index()
        {
            if (HttpContext.Session.GetString("Usuario") == null)
                return RedirectToAction("Login", "Account");

            var permisos = HttpContext.Session.GetString("Permisos") ?? "";
            ViewBag.Permisos = permisos;

            var modulos = new List<Modulo>
            {
                new Modulo { Permiso = "Operadora", Titulo = "Operadora", Icono = "📞", Url = "/Operadora/Index" },
                new Modulo { Permiso = "Botones", Titulo = "Botones", Icono = "🔑", Url = "/Botones/Index" },
                new Modulo { Permiso = "Movimientos", Titulo = "Movimientos", Icono = "✏️", Url = "/Botones/EditarRegistro" },
                new Modulo { Permiso = "PuertaSol", Titulo = "Puerta del Sol", Icono = "🚪", Url = "/Tickets/Reservas" },
                new Modulo { Permiso = "Dashboard", Titulo = "Usuarios", Icono = "👤", Url = "/Usuarios/Index" },
                new Modulo { Permiso = "Reportes", Titulo = "Reportes", Icono = "📊", Url = "/Reportes/Index" },
                new Modulo { Permiso = "Configuracion", Titulo = "Config Tickets", Icono = "⚙️", Url = "/Tickets/Config" }
            };

            ViewBag.Modulos = modulos;

            return View();
        }
    }
}