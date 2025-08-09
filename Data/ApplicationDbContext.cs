using Microsoft.EntityFrameworkCore;
using AppValetParking.Models;

namespace AppValetParking.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options) { }

        public DbSet<Usuario> Usuarios { get; set; }
        public DbSet<ValetRegistro> ValetRegistros { get; set; } // <- Esta línea es clave
        public DbSet<ValetMovimiento> ValetMovimientos { get; set; }

    }
}
