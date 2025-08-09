using Microsoft.EntityFrameworkCore;
using AppValetParking.Models; // Ajusta si es otro namespace

public class PegasysDbContext : DbContext
{
    public PegasysDbContext(DbContextOptions<PegasysDbContext> options) : base(options) { }

    public DbSet<VV_TARJETAS_EMPLEADOS> VV_TARJETAS_EMPLEADOS { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<VV_TARJETAS_EMPLEADOS>()
            .HasNoKey()
            .ToView("VV_TARJETAS_EMPLEADOS");
    }
}
