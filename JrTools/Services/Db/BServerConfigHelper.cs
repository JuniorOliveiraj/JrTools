using JrTools.Dto;
using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace JrTools.Services.Db
{
    public static class BServerConfigHelper
    {
        private static string GetPath()
        {
            var folder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "JrTools");
            if (!Directory.Exists(folder))
                Directory.CreateDirectory(folder);
            return Path.Combine(folder, "bserver-config.json");
        }

        public static async Task<BServerConfigDto> LerAsync()
        {
            var path = GetPath();
            if (!File.Exists(path))
                return new BServerConfigDto();
            try
            {
                var json = await File.ReadAllTextAsync(path);
                return JsonSerializer.Deserialize<BServerConfigDto>(json)
                       ?? new BServerConfigDto();
            }
            catch { return new BServerConfigDto(); }
        }

        public static async Task SalvarAsync(BServerConfigDto config)
        {
            var json = JsonSerializer.Serialize(config,
                new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(GetPath(), json);
        }
    }
}