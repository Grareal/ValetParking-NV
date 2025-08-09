    using Microsoft.AspNetCore.Mvc;
    using Microsoft.EntityFrameworkCore;
    using AppValetParking.Data;
    using AppValetParking.Models;

    namespace AppValetParking.Controllers
    {
        [Route("api/[controller]")]
        [ApiController]
        public class EmpleadosController : ControllerBase
        {
            private readonly PegasysDbContext _pegasysContext;

            public EmpleadosController(PegasysDbContext pegasysContext)
            {
                _pegasysContext = pegasysContext;
            }

            [HttpGet("{id}")]
            public async Task<IActionResult> GetEmpleadoPorId(string id)
            {
                var emp = await _pegasysContext.VV_TARJETAS_EMPLEADOS
                    .FirstOrDefaultAsync(e => e.ID_ICLASS == id);

                if (emp == null)
                    return NotFound();

          


                var nombreCompleto = $"{emp.c_mname} {emp.c_lname}";

                return Ok(new
                {
                    nombre = nombreCompleto.Trim(),
                
                });
            }

            [HttpGet("api/Empleados/BuscarPorCodigo")]
            public async Task<IActionResult> BuscarPorCodigo(string codigo)
            {
                var emp = await _pegasysContext.VV_TARJETAS_EMPLEADOS
                    .FirstOrDefaultAsync(e => e.ID_ICLASS == codigo); // Cambia aquí según el campo que estás usando

                if (emp == null)
                    return NotFound();

                var nombreCompleto = $"{emp.c_mname} {emp.c_lname}".Trim();

                return Ok(new
                {
                    nombreCompleto
                });
            }

        }
    }
