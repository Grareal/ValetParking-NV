using Microsoft.EntityFrameworkCore;
using AppValetParking.Models;

public class TcabdopeNewDbContext : DbContext
{
    public TcabdopeNewDbContext(DbContextOptions<TcabdopeNewDbContext> options) : base(options) { }

    public DbSet<Acompanante> Acompanantes { get; set; }
    public DbSet<Reserva> Reservas { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Acompanante>().ToTable("hothch", "dbo").HasNoKey();
        modelBuilder.Entity<Reserva>().ToTable("hothsp", "dbo").HasNoKey();
    }
}
