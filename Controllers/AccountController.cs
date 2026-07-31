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

        // Agregar en AccountController
        [HttpPost]
        [IgnoreAntiforgeryToken]
        public IActionResult LoginApi([FromBody] LoginRequest request)
        {
            var usuario = _context.Usuarios
                .FirstOrDefault(u => u.Username == request.Username
                                  && u.Password == request.Password);

            if (usuario == null)
                return Unauthorized(new { error = "Usuario o contrase�a incorrectos." });

            return Json(new
            {
                id = usuario.Id,
                username = usuario.Username,
                nombre = usuario.Nombre,   // nombre real para el saludo en la app
                funciones = usuario.Funciones,
                gaffete = usuario.Gafete  // el numeroOperador
            });
        }

        // Clase para recibir el body JSON
        public class LoginRequest
        {
            public string Username { get; set; }
            public string Password { get; set; }
        }

        [HttpPost]
        public IActionResult Login(string username, string password)
        {
            var usuario = _context.Usuarios
                .FirstOrDefault(u => u.Username == username && u.Password == password);

            if (usuario == null)
            {
                ViewBag.Error = "Usuario o contrase�a incorrectos.";
                return View();
            }

            // Guardar sesi�n
            HttpContext.Session.SetString("Usuario", usuario.Username);
            HttpContext.Session.SetString("Permisos", usuario.Funciones);

            // Ir al men� principal
            return RedirectToAction("Index", "Dashboard");
        }

    }
}