using AppValetParking.Data;
using Microsoft.EntityFrameworkCore;

namespace AppValetParking.Services;

public static class DatabaseInitializer
{
    public static async Task InicializarAccesosAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        foreach (var nombre in new[] { "AccesosVistas.sql", "SolicitudesSupervision.sql" })
        {
            var sqlPath = Path.Combine(AppContext.BaseDirectory, "Sql", nombre);
            if (File.Exists(sqlPath))
                await db.Database.ExecuteSqlRawAsync(await File.ReadAllTextAsync(sqlPath));
        }
    }
}
