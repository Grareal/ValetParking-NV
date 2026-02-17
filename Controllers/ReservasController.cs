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

    public async Task<IActionResult> Index(
        DateTime? fechaInicio,
        DateTime? fechaFin,
        int page = 1,
        int pageSize = 100,
        string vista = "llegadas")
    {
        var fechaHoy = DateTime.Today;
        string hoyStr = fechaHoy.ToString("yyyyMMdd");

        // LLEGADAS DEL DÍA
        var llegadasDelDia = await _context.Reservas
            .AsNoTracking()
            .Where(r => r.h_fec_lld == hoyStr)
            .ToListAsync();

        var llegadasResCves = llegadasDelDia
            .Select(r => r.h_res_cve)
            .Where(c => !string.IsNullOrEmpty(c))
            .ToList();

        var llegadasCodReservas = llegadasDelDia
            .Select(r => r.h_cod_reserva)
            .Where(c => !string.IsNullOrEmpty(c))
            .ToList();

        var acompanantesLlegadas = await _context.Acompanantes
            .AsNoTracking()
            .Where(a => llegadasResCves.Contains(a.h_res_cve)
                     || llegadasCodReservas.Contains(a.h_cod_reserva))
            .ToListAsync();

        var acompanantesLlegadasDict = acompanantesLlegadas
            .GroupBy(a => a.h_res_cve ?? a.h_cod_reserva)
            .ToDictionary(g => g.Key, g => g.Select(a => a.h_nom).ToList());

        foreach (var llegada in llegadasDelDia)
        {
            string clave = llegada.h_res_cve ?? llegada.h_cod_reserva;

            llegada.h_status = TraducirStatus(llegada.h_status);
            llegada.h_fec_lld = FormatearFechaStr(llegada.h_fec_lld);
            llegada.h_fec_sda = FormatearFechaStr(llegada.h_fec_sda);
            llegada.NombresAcompanantes = acompanantesLlegadasDict.ContainsKey(clave)
                ? acompanantesLlegadasDict[clave]
                : new List<string>();
            llegada.AcompanantesTexto = CrearTextoAcompanantes(llegada.NombresAcompanantes);
        }

        // --------------------
        // RESERVAS DEL MES
        // --------------------
        var inicioFiltro = fechaInicio?.Date ?? new DateTime(fechaHoy.Year, fechaHoy.Month, 1);
        var finFiltro = fechaFin?.Date ?? new DateTime(fechaHoy.Year, fechaHoy.Month, DateTime.DaysInMonth(fechaHoy.Year, fechaHoy.Month));

        string inicioStr = inicioFiltro.ToString("yyyyMMdd");
        string finStr = finFiltro.ToString("yyyyMMdd");

        var reservas = await _context.Reservas
            .AsNoTracking()
            .Where(r =>
                !string.IsNullOrEmpty(r.h_fec_lld) &&
                r.h_fec_lld.CompareTo(inicioStr) >= 0 &&
                r.h_fec_lld.CompareTo(finStr) <= 0
            )
            .OrderByDescending(r => r.h_fec_lld)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var resCves = reservas.Select(r => r.h_res_cve).Where(c => !string.IsNullOrEmpty(c)).ToList();
        var codReservas = reservas.Select(r => r.h_cod_reserva).Where(c => !string.IsNullOrEmpty(c)).ToList();

        var acompanantesList = await _context.Acompanantes
            .AsNoTracking()
            .Where(a => resCves.Contains(a.h_res_cve)
                     || codReservas.Contains(a.h_cod_reserva))
            .ToListAsync();

        var acompanantesDict = acompanantesList
            .GroupBy(a => a.h_res_cve ?? a.h_cod_reserva)
            .ToDictionary(g => g.Key, g => g.Select(a => a.h_nom).ToList());

        foreach (var reserva in reservas)
        {
            string clave = reserva.h_res_cve ?? reserva.h_cod_reserva;

            reserva.h_status = TraducirStatus(reserva.h_status);
            reserva.h_fec_lld = FormatearFechaStr(reserva.h_fec_lld);
            reserva.h_fec_sda = FormatearFechaStr(reserva.h_fec_sda);
            reserva.NombresAcompanantes = acompanantesDict.ContainsKey(clave)
                ? acompanantesDict[clave]
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

    

    // -----------------------------
    // BUSCAR RESERVAS
    // -----------------------------
    [HttpGet]
    public async Task<IActionResult> BuscarReservas(
     string texto,
     DateTime? fechaInicio,
     DateTime? fechaFin)
    {
        var query = _context.Reservas.AsNoTracking().AsQueryable();

        // Filtrar por fechas si hay
        if (fechaInicio.HasValue)
        {
            string fi = fechaInicio.Value.ToString("yyyyMMdd");
            query = query.Where(r => r.h_fec_lld.CompareTo(fi) >= 0);
        }

        if (fechaFin.HasValue)
        {
            string ff = fechaFin.Value.ToString("yyyyMMdd");
            query = query.Where(r => r.h_fec_lld.CompareTo(ff) <= 0);
        }

        // Filtrar por texto si hay
        if (!string.IsNullOrWhiteSpace(texto))
        {
            string patron = $"%{texto.Trim()}%";
            query = query.Where(r =>
                EF.Functions.Like(r.h_nom, patron) ||
                EF.Functions.Like(r.h_res_cve, patron) ||
                EF.Functions.Like(r.h_cod_reserva, patron)
            );
        }

        var reservas = await query
            .OrderByDescending(r => r.h_fec_lld)
            .Take(500)
            .ToListAsync();

        // Obtener acompañantes de todas las reservas
        var claves = reservas.Select(r => r.h_res_cve ?? r.h_cod_reserva)
                             .Where(c => !string.IsNullOrEmpty(c))
                             .ToList();

        var acompanantes = await _context.Acompanantes
            .AsNoTracking()
            .Where(a => claves.Contains(a.h_res_cve) || claves.Contains(a.h_cod_reserva))
            .ToListAsync();

        var dict = acompanantes
            .GroupBy(a => a.h_res_cve ?? a.h_cod_reserva)
            .ToDictionary(g => g.Key, g => g.Select(x => x.h_nom).ToList());

        // Transformamos todo antes de devolver JSON
        var resultado = reservas.Select(r => new
        {
            h_status = TraducirStatus(r.h_status),
            r.h_res_cve,
            r.h_cod_reserva,
            r.h_nom,
            h_fec_lld = FormatearFechaStr(r.h_fec_lld),
            h_fec_sda = FormatearFechaStr(r.h_fec_sda),
            r.Hotel,
            AcompanantesTexto = CrearTextoAcompanantes(
                dict.ContainsKey(r.h_res_cve ?? r.h_cod_reserva)
                    ? dict[r.h_res_cve ?? r.h_cod_reserva]
                    : new List<string>()
            )
        });

        return Json(resultado);
    }

    // -----------------------------
    // Métodos auxiliares
    // -----------------------------
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
            DateTime.TryParseExact(fecha, "yyyyMMdd", null,
                System.Globalization.DateTimeStyles.None,
                out var fechaFormateada))
        {
            return fechaFormateada.ToString("dd/MM/yyyy");
        }

        return fecha;
    }

    private string CrearTextoAcompanantes(List<string> nombres)
    {
        if (nombres == null || nombres.Count == 0)
            return "Sin acompañantes";

        int maxVisible = 3;
        var visibles = nombres.Take(maxVisible).ToList();
        int restantes = nombres.Count - maxVisible;

        string displayText = string.Join(", ", visibles);
        if (restantes > 0)
            displayText += $" (+{restantes} más)";

        return displayText;
    }

}
