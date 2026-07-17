using JrTools.Dto;
using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace JrTools.Services.Db
{
    public static class NotasHelper
    {
        private static string GetPath()
        {
            var folder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "JrTools");
            if (!Directory.Exists(folder))
                Directory.CreateDirectory(folder);
            return Path.Combine(folder, "notas.json");
        }

        public static async Task<NotasDataObject> LerAsync()
        {
            var path = GetPath();
            if (!File.Exists(path))
                return new NotasDataObject();
            try
            {
                var json = await File.ReadAllTextAsync(path);
                return JsonSerializer.Deserialize<NotasDataObject>(json) ?? new NotasDataObject();
            }
            catch { return new NotasDataObject(); }
        }

        public static async Task SalvarAsync(NotasDataObject data)
        {
            var json = JsonSerializer.Serialize(data,
                new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(GetPath(), json);
        }
    }
}
