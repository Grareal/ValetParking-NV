using Microsoft.EntityFrameworkCore;
using AppValetParking.Models;

namespace AppValetParking.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options) { }

        public DbSet<Usuario> Usuarios { get; set; }
        public DbSet<ValetRegistro> ValetRegistros { get; set; } 
        public DbSet<ValetMovimiento> ValetMovimientos { get; set; }
        public DbSet<ValetSolicitud> ValetSolicitudes { get; set; }
        public DbSet<Cajon> Cajones { get; set; }
        public DbSet<TicketEnviado> TicketsEnviados { get; set; }
        public DbSet<VehiculoInfo> VehiculosInfo { get; set; }
        public DbSet<VehiculoFoto> VehiculoFotos { get; set; }


    }
}
