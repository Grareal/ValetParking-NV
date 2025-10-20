using System.IO;
using System.Text.Json;
using AppValetParking.Models;

namespace AppValetParking.Services
{
    public class PrinterConfigService
    {
        private readonly string _filePath;

        public PrinterConfigService(IWebHostEnvironment env)
        {
            var configFolder = Path.Combine(env.WebRootPath ?? env.ContentRootPath, "config");
            if (!Directory.Exists(configFolder)) Directory.CreateDirectory(configFolder);
            _filePath = Path.Combine(configFolder, "printers.json");
        }

        public List<PrinterConfig> GetAll()
        {
            if (!File.Exists(_filePath)) return new List<PrinterConfig>();
            var txt = File.ReadAllText(_filePath);
            if (string.IsNullOrWhiteSpace(txt)) return new List<PrinterConfig>();
            return JsonSerializer.Deserialize<List<PrinterConfig>>(txt) ?? new List<PrinterConfig>();
        }

        public void SaveAll(List<PrinterConfig> configs)
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            File.WriteAllText(_filePath, JsonSerializer.Serialize(configs, options));
        }

        public void AddOrUpdate(PrinterConfig config)
        {
            var list = GetAll();
            var existing = list.FirstOrDefault(x => x.Hostname.Equals(config.Hostname, StringComparison.OrdinalIgnoreCase));
            if (existing != null)
            {
                existing.Name = config.Name;
                existing.Printers = config.Printers;
                existing.PrintGroups = config.PrintGroups;
            }
            else
            {
                list.Add(config);
            }
            SaveAll(list);
        }

        public void Remove(string hostname)
        {
            var list = GetAll();
            list.RemoveAll(x => x.Hostname.Equals(hostname, StringComparison.OrdinalIgnoreCase));
            SaveAll(list);
        }
    }
}
