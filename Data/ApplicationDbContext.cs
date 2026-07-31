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
        public DbSet<CodigoLiberacion> CodigosLiberacion { get; set; }
        public DbSet<FolioTransferido> FoliosTransferidos { get; set; }
        public DbSet<VistaSistema> VistasSistema { get; set; }
        public DbSet<RolVista> RolVistas { get; set; }
        public DbSet<SolicitudAuditoria> SolicitudesAuditoria { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<VistaSistema>().HasIndex(v => v.Clave).IsUnique();
            modelBuilder.Entity<RolVista>().HasIndex(rv => new { rv.Rol, rv.VistaSistemaId }).IsUnique();
            modelBuilder.Entity<RolVista>()
                .HasOne(rv => rv.VistaSistema)
                .WithMany(v => v.Roles)
                .HasForeignKey(rv => rv.VistaSistemaId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
