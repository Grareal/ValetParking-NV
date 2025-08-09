using System;
using System.Collections.Generic;

namespace AppValetParking.Models
{
	public class ReservasViewModel
	{
		public List<Reserva> LlegadasDelDia { get; set; } = new();
		public List<Reserva> Reservas { get; set; } = new();

		// Propiedades para filtros y paginación
		public string? NombreEmpresaSeleccionada { get; set; }
		public DateTime? FechaInicio { get; set; }
		public DateTime? FechaFin { get; set; }

		// Para el formulario de fechas (ya las usas)
		public DateTime FechaInicioFiltro { get; set; }
		public DateTime FechaFinFiltro { get; set; }

		public int CurrentPage { get; set; }
		public int TotalPages { get; set; }
	}
}
