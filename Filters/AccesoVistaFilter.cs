using AppValetParking.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace AppValetParking.Filters;

public class AccesoVistaFilter : IAsyncActionFilter
{
    private readonly AccesoVistasService _accesos;

    public AccesoVistaFilter(AccesoVistasService accesos) => _accesos = accesos;

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var path = context.HttpContext.Request.Path.Value ?? "";
        if (path.StartsWith("/Account", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/api", StringComparison.OrdinalIgnoreCase))
        {
            await next();
            return;
        }

        var roles = context.HttpContext.Session.GetString("Permisos");
        if (!await _accesos.PuedeAccederAsync(path, roles))
        {
            context.Result = new RedirectToActionResult("Index", "Dashboard", new { accesoDenegado = true });
            return;
        }

        // Compatibilidad temporal con el atributo historico de Tickets/Codigos.
        // El catalogo BD sigue siendo quien decide el acceso real.
        if (path.Equals("/Tickets/Codigos", StringComparison.OrdinalIgnoreCase) &&
            AccesoVistasService.SepararRoles(roles).Contains("OperadoraValet") &&
            !(roles ?? "").Contains("Configuracion"))
        {
            context.HttpContext.Session.SetString("Permisos", $"{roles},Configuracion");
        }

        await next();
    }
}
