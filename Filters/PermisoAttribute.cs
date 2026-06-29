using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace AppValetParking.Filters
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
    public class PermisoAttribute : ActionFilterAttribute
    {
        private readonly string[] _permisos;

        // Recibe uno o varios permisos
        public PermisoAttribute(params string[] permisos)
        {
            _permisos = permisos;
        }

        public override void OnActionExecuting(ActionExecutingContext context)
        {
            var session = context.HttpContext.Session;
            var permisosUsuario = session.GetString("Permisos") ?? "";
            var listaPermisos = permisosUsuario.Split(',', StringSplitOptions.RemoveEmptyEntries);

            // Si el usuario tiene TI o Administracion, pasa automático
            if (listaPermisos.Contains("Administracion") || listaPermisos.Contains("TI"))
            {
                base.OnActionExecuting(context);
                return;
            }

            // Si tiene alguno de los permisos requeridos, pasa
            bool tieneAcceso = _permisos.Any(p => listaPermisos.Contains(p));

            if (!tieneAcceso)
            {
                // Redirige al dashboard si no tiene permiso
                context.Result = new RedirectToActionResult("Index", "Dashboard", null);
            }
        }
    }
}