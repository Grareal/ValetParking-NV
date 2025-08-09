using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using AppValetParking.Models;

namespace AppValetParking.Services
{
    public static class ServicioHelper
    {
        public static List<ServicioItem> ObtenerServicios(string rutaCompleta)
        {
            if (!File.Exists(rutaCompleta)) return new List<ServicioItem>();

            var json = File.ReadAllText(rutaCompleta);
            return JsonSerializer.Deserialize<List<ServicioItem>>(json);
        }
    }
}
