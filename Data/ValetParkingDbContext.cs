using Microsoft.EntityFrameworkCore;
using AppValetParking.Models;

namespace AppValetParking.Data
{
    public class ValetParkingDbContext : DbContext
    {
        public ValetParkingDbContext(DbContextOptions<ValetParkingDbContext> options)
            : base(options)
        {
        }

        public DbSet<TicketEnviado> TicketsEnviados { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<TicketEnviado>().ToTable("TicketsEnviados", "dbo");
        }
    }
}