

    using Microsoft.AspNetCore.Mvc;
using AppValetParking.Data;
using AppValetParking.Models;
using Microsoft.EntityFrameworkCore;

public class ReservasController : Controller
{
    private readonly TcabdopeNewDbContext _context;

    public ReservasController(TcabdopeNewDbContext context)
    {
        _context = context;
    }


    public async Task<IActionResult> Index(DateTime? fechaInicio, DateTime? fechaFin, int page = 1, int pageSize = 100, string vista = "llegadas")
    {
        var fechaHoy = DateTime.Today;

        // ----------------------
        // Llegadas del día
        // ----------------------
        var llegadasDelDia = await _context.Reservas
            .Where(r => r.h_fec_lld == fechaHoy.ToString("yyyyMMdd"))
            .ToListAsync();

        foreach (var llegada in llegadasDelDia)
        {
            llegada.h_status = TraducirStatus(llegada.h_status);
            llegada.h_fec_lld = FormatearFechaStr(llegada.h_fec_lld);
            llegada.h_fec_sda = FormatearFechaStr(llegada.h_fec_sda);

            llegada.NombresAcompanantes = await _context.Acompanantes
                .Where(a => a.h_res_cve == llegada.h_res_cve)
                .Select(a => a.h_nom)
                .ToListAsync();

            llegada.AcompanantesTexto = CrearTextoAcompanantes(llegada.NombresAcompanantes);
        }
        // ----------------------
        // Reservas del mes (filtradas sin paginación)
        // ----------------------
        var inicioFiltro = fechaInicio?.Date ?? new DateTime(fechaHoy.Year, fechaHoy.Month, 1);
        var finFiltro = fechaFin?.Date ?? new DateTime(fechaHoy.Year, fechaHoy.Month, DateTime.DaysInMonth(fechaHoy.Year, fechaHoy.Month));

        var reservas = await _context.Reservas
            .Where(r =>
                !string.IsNullOrEmpty(r.h_fec_lld) &&
                r.h_fec_lld.Length == 8 &&
                string.Compare(r.h_fec_lld, inicioFiltro.ToString("yyyyMMdd")) >= 0 &&
                string.Compare(r.h_fec_lld, finFiltro.ToString("yyyyMMdd")) <= 0
            )
            .OrderByDescending(r => r.h_fec_lld)
            .ToListAsync();

        // Obtener acompañantes
        var resCves = reservas.Select(r => r.h_res_cve).Where(c => !string.IsNullOrEmpty(c)).ToList();

        var acompanantesList = await _context.Acompanantes
            .Where(a => !string.IsNullOrEmpty(a.h_res_cve) && resCves.Contains(a.h_res_cve))
            .ToListAsync();

        var acompanantesDict = acompanantesList
            .GroupBy(a => a.h_res_cve)
            .ToDictionary(g => g.Key, g => g.Select(a => a.h_nom).ToList());

        foreach (var reserva in reservas)
        {
            reserva.h_status = TraducirStatus(reserva.h_status);
            reserva.h_fec_lld = FormatearFechaStr(reserva.h_fec_lld);
            reserva.h_fec_sda = FormatearFechaStr(reserva.h_fec_sda);

            reserva.NombresAcompanantes = acompanantesDict.ContainsKey(reserva.h_res_cve)
                ? acompanantesDict[reserva.h_res_cve]
                : new List<string>();

            reserva.AcompanantesTexto = CrearTextoAcompanantes(reserva.NombresAcompanantes);
        }


        var viewModel = new ReservasViewModel
        {
            LlegadasDelDia = llegadasDelDia,
            Reservas = reservas,
            FechaInicioFiltro = inicioFiltro,
            FechaFinFiltro = finFiltro
        };

        ViewData["VistaActual"] = vista;
        return View(viewModel);

    }

    private string CrearTextoAcompanantes(List<string> nombres)
    {
        if (nombres == null || nombres.Count == 0)
            return "Sin acompañantes";


        return string.Join(", ", nombres);
    }



    [HttpGet]
    public async Task<IActionResult> BuscarReservas(string texto, DateTime? fechaInicio, DateTime? fechaFin)
    {
        var query = _context.Reservas.AsQueryable();

        if (fechaInicio.HasValue)
        {
            string fi = fechaInicio.Value.ToString("yyyyMMdd");
            query = query.Where(r => !string.IsNullOrEmpty(r.h_fec_lld) && string.Compare(r.h_fec_lld, fi) >= 0);
        }

        if (fechaFin.HasValue)
        {
            string ff = fechaFin.Value.ToString("yyyyMMdd");
            query = query.Where(r => !string.IsNullOrEmpty(r.h_fec_lld) && string.Compare(r.h_fec_lld, ff) <= 0);
        }

        if (!string.IsNullOrWhiteSpace(texto))
        {
            string patron = $"%{texto.Trim()}%";
            query = query.Where(r =>
                (r.h_nom != null && EF.Functions.Like(r.h_nom, patron)) ||
                (r.h_res_cve != null && EF.Functions.Like(r.h_res_cve, patron)) ||
                (r.h_cod_reserva != null && EF.Functions.Like(r.h_cod_reserva, patron))
            );
        }

        // Suponiendo que 'query' es tu IQueryable<Reserva> con filtros aplicados

        var reservas = await query
     .OrderByDescending(r => r.h_fec_lld)
     .ThenBy(r => r.h_cod_reserva)
     .Take(500)
     .ToListAsync();

        var resCves = reservas
            .Select(r => r.h_res_cve)
            .Where(r => !string.IsNullOrEmpty(r))
            .ToList();

        if (!resCves.Any())
        {
            return Json(new List<object>());
        }

        // Traemos la lista completa primero, para evitar problemas con EF Core al hacer Contains con listas vacías o nulas
        var acompanantesList = await _context.Acompanantes
            .Where(a => !string.IsNullOrEmpty(a.h_res_cve) && resCves.Contains(a.h_res_cve) && !string.IsNullOrEmpty(a.h_nom))
            .ToListAsync();

        var acompanantesDict = acompanantesList
            .GroupBy(a => a.h_res_cve)
            .ToDictionary(
                g => g.Key,
                g => g.Select(x => x.h_nom!).ToList()
            );

        var resultado = reservas.Select(r => new
        {
            r.h_status,
            r.h_res_cve,
            r.h_cod_reserva,
            r.h_nom,
            h_fec_lld = FormatearFechaStr(r.h_fec_lld),
            h_fec_sda = FormatearFechaStr(r.h_fec_sda),
            r.Hotel,
            Acompanantes = acompanantesDict.ContainsKey(r.h_res_cve) ? acompanantesDict[r.h_res_cve] : new List<string>()
        });

        return Json(resultado);



    }


    private string TraducirStatus(string codigo)
    {
        return codigo switch
        {
            "00" => "RESERVED",
            "50" => "CHECKED OUT",
            "01" => "CANCELLED",
            "02" => "NO SHOW",
            "10" => "CHECKED IN",
            _ => "DESCONOCIDO"
        };
    }

    private string FormatearFechaStr(string fecha)
    {
        if (!string.IsNullOrEmpty(fecha) && fecha.Length == 8 &&
            DateTime.TryParseExact(fecha, "yyyyMMdd", null, System.Globalization.DateTimeStyles.None, out var fechaFormateada))
        {
            return fechaFormateada.ToString("dd/MM/yyyy");
        }

        return fecha; // Si no se puede convertir, devuélvela como está
    }



    [HttpGet]
    public async Task<IActionResult> GetAcompanantes(string reserva)
    {
        if (string.IsNullOrEmpty(reserva))
            return BadRequest("Código de reserva requerido.");

        var acompanantes = await _context.Acompanantes
    .Where(h => h.h_res_cve == reserva || h.h_cod_reserva == reserva)
    .Select(h => new { h.h_nom })
    .ToListAsync();


        return Json(acompanantes);
    }






}
