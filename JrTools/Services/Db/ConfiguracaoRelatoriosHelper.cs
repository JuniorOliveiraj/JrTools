using JrTools.Dto;
using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace JrTools.Services.Db
{
    public static class ConfiguracaoRelatoriosHelper
    {
        private static string GetPath()
        {
            var folder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "JrTools");
            if (!Directory.Exists(folder))
                Directory.CreateDirectory(folder);
            return Path.Combine(folder, "ConfiguracaoRelatoriosRh.json");
        }

        public static async Task<ConfiguracaoRelatoriosRh> LerAsync()
        {
            var path = GetPath();
            if (!File.Exists(path))
                return new ConfiguracaoRelatoriosRh();
            try
            {
                var json = await File.ReadAllTextAsync(path);
                return JsonSerializer.Deserialize<ConfiguracaoRelatoriosRh>(json)
                       ?? new ConfiguracaoRelatoriosRh();
            }
            catch { return new ConfiguracaoRelatoriosRh(); }
        }

        public static async Task SalvarAsync(ConfiguracaoRelatoriosRh config)
        {
            var json = JsonSerializer.Serialize(config,
                new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(GetPath(), json);
        }
    }
}
