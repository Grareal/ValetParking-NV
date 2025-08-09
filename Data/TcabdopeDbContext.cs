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
            entity.ToView("rep_reservation_all_view", "dbo");
        });
    }
}
