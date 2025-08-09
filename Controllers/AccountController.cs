using AppValetParking.Data;
using AppValetParking.Models;
using Microsoft.AspNetCore.Mvc;
using System.Linq;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;



namespace AppValetParking.Controllers
{
    public class AccountController : Controller
    {
        private readonly ApplicationDbContext _context;

        public AccountController(ApplicationDbContext context)
        {
            _context = context;
        }

        public IActionResult Login()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync();

            HttpContext.Session.Clear();

            // Redirige a login
            return RedirectToAction("Login", "Account");
        }

        [HttpPost]
        public IActionResult Login(string username, string password)
        {
            var usuario = _context.Usuarios.FirstOrDefault(u => u.Username == username && u.Password == password);

            if (usuario == null)
            {
                ViewBag.Error = "Usuario o contraseña incorrectos.";
                return View();
            }

            switch (usuario.Funciones)
            {
                case "Operadora":
                    return RedirectToAction("Index", "Operadora");
                case "Botones":
                    return RedirectToAction("Index", "Botones");
                case "Movimientos":
                    return RedirectToAction("EditarRegistro", "Botones");

                case "PuertaSol":
                    return RedirectToAction("Index", "Reservas");
                default:
                    ViewBag.Error = "Función no autorizada.";
                    return View();
            }
        }

    }
}