using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AppValetParking.Data;
using AppValetParking.Models;

namespace AppValetParking.Controllers
{
    /// <summary>
    /// Transferencia de folio: reasigna toda la información de un folio viejo
    /// (etiqueta perdida/dañada) a un folio nuevo, conservando el anterior en el
    /// historial (FoliosTransferidos) para que siga siendo consultable.
    /// </summary>
    [ApiController]
    [Route("api/folios")]
    public class FoliosController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly PegasysDbContext _pegasysContext;

        public FoliosController(ApplicationDbContext context, PegasysDbContext pegasysContext)
        {
            _context = context;
            _pegasysContext = pegasysContext;
        }

        public class TransferirDto
        {
            public string? FolioAnterior { get; set; }
            public string? FolioNuevo { get; set; }
            public string? Motivo { get; set; }
            public string? NumeroOperador { get; set; }
        }

        [HttpPost("transferir")]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> Transferir([FromBody] TransferirDto dto)
        {
            var anterior = (dto.FolioAnterior ?? "").Trim().ToUpperInvariant();
            var nuevo = (dto.FolioNuevo ?? "").Trim().ToUpperInvariant();
            var motivo = (dto.Motivo ?? "").Trim();

            // ── Validaciones básicas ─────────────────────────────
            if (string.IsNullOrWhiteSpace(anterior) || string.IsNullOrWhiteSpace(nuevo))
                return Ok(new { success = false, mensaje = "Folio anterior y folio nuevo son obligatorios." });

            if (anterior == nuevo)
                return Ok(new { success = false, mensaje = "El folio nuevo debe ser distinto al anterior." });

            // El folio viejo debe existir (registro o vehículo).
            bool existeViejo = await _context.ValetRegistros.AnyAsync(r => r.FolioVP == anterior)
                            || await _context.VehiculosInfo.AnyAsync(v => v.FolioVP == anterior);
            if (!existeViejo)
                return Ok(new { success = false, mensaje = $"El folio anterior '{anterior}' no existe." });

            // El folio nuevo debe estar libre.
            if (await FolioOcupado(nuevo))
                return Ok(new { success = false, mensaje = $"El folio nuevo '{nuevo}' ya está en uso. Usa otro." });

            // Si hay segundo vehículo (folio + "B"), su destino también debe estar libre.
            bool haySegundo = await FolioOcupado($"{anterior}B");
            if (haySegundo && await FolioOcupado($"{nuevo}B"))
                return Ok(new { success = false, mensaje = $"El folio nuevo del 2º vehículo '{nuevo}B' ya está en uso." });

            // ── Nombre del operador (Pegasys) ────────────────────
            var operador = await ResolverOperador(dto.NumeroOperador);

            // ── Transacción: renombrar + historial + movimiento ──
            await using var tx = await _context.Database.BeginTransactionAsync();
            try
            {
                await RenombrarFolio(anterior, nuevo, motivo, operador);
                if (haySegundo)
                    await RenombrarFolio($"{anterior}B", $"{nuevo}B", motivo, operador);

                await _context.SaveChangesAsync();
                await tx.CommitAsync();
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync();
                return Ok(new { success = false, mensaje = "No se pudo transferir: " + ex.Message });
            }

            return Ok(new
            {
                success = true,
                mensaje = $"Folio transferido de {anterior} a {nuevo}.",
                folioAnterior = anterior,
                folioNuevo = nuevo,
                transfirioSegundo = haySegundo
            });
        }

        /// <summary>Renombra un folio en todas las tablas y deja rastro.</summary>
        private async Task RenombrarFolio(string de, string a, string motivo, string operador)
        {
            var now = DateTime.Now;

            // ValetRegistros: se cargan (tracked) para poder anexar el texto de
            // movimiento y crear el ValetMovimiento con su IdRegistro.
            var registros = await _context.ValetRegistros
                .Where(r => r.FolioVP == de)
                .ToListAsync();

            string textoMov = $"|CAMBIO DE FOLIO {de} -> {a} {now:yyyy-MM-dd HH:mm} por {operador}" +
                              (string.IsNullOrWhiteSpace(motivo) ? "" : $": {motivo}") + "|";

            foreach (var reg in registros)
            {
                reg.FolioVP = a;
                reg.Movimientos = (reg.Movimientos ?? "") + textoMov;
                _context.ValetMovimientos.Add(new ValetMovimiento
                {
                    IdRegistro = reg.Id,
                    Reserva = reg.Reserva,
                    Servicio = reg.Servicio,
                    FechaHora = now,
                    Operador = operador,
                    MovimientoTexto = textoMov
                });
            }

            // Resto de tablas: update directo (no requieren tracking).
            // NOTA: VehiculoFotos.RutaArchivo se deja igual a propósito — los
            // archivos físicos siguen en /uploads/{folioViejo}/ y cargan bien.
            await _context.VehiculosInfo
                .Where(v => v.FolioVP == de)
                .ExecuteUpdateAsync(s => s.SetProperty(v => v.FolioVP, a));

            await _context.VehiculoFotos
                .Where(f => f.FolioVP == de)
                .ExecuteUpdateAsync(s => s.SetProperty(f => f.FolioVP, a));

            await _context.ValetSolicitudes
                .Where(sol => sol.FolioVP == de)
                .ExecuteUpdateAsync(s => s.SetProperty(sol => sol.FolioVP, a));

            _context.FoliosTransferidos.Add(new FolioTransferido
            {
                FolioAnterior = de,
                FolioNuevo = a,
                Motivo = string.IsNullOrWhiteSpace(motivo) ? null : motivo,
                Operador = operador,
                Fecha = now
            });
        }

        private async Task<bool> FolioOcupado(string folio)
        {
            return await _context.ValetRegistros.AnyAsync(r => r.FolioVP == folio)
                || await _context.VehiculosInfo.AnyAsync(v => v.FolioVP == folio);
        }

        private async Task<string> ResolverOperador(string? numeroOperador)
        {
            if (string.IsNullOrWhiteSpace(numeroOperador))
                return "Operador no indicado";
            try
            {
                var nombre = await _pegasysContext.VV_TARJETAS_EMPLEADOS
                    .Where(v => v.ID_ICLASS == numeroOperador)
                    .Select(v => v.c_mname + " " + v.c_lname)
                    .FirstOrDefaultAsync();
                return string.IsNullOrWhiteSpace(nombre) ? numeroOperador : nombre.Trim();
            }
            catch
            {
                return numeroOperador;
            }
        }
    }
}
