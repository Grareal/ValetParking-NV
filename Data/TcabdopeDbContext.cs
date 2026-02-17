using Microsoft.EntityFrameworkCore;

using AppValetParking.Models;

public class TcabdopeDbContext : DbContext
{
    public TcabdopeDbContext(DbContextOptions<TcabdopeDbContext> options) : base(options) { }

    public DbSet<ReservationAllView> ReservationAllView { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ReservationAllView>(entity =>
        {
            entity.HasNoKey();
            //vw_reservations_with_confno
            entity.ToView("vw_reservations_cloud", "dbo");
        });
    }
}
