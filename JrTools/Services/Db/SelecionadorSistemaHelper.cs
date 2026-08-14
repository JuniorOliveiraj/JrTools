using JrTools.Dto;
using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace JrTools.Services.Db
{
    public static class SelecionadorSistemaHelper
    {
        private static string GetConfigPath()
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "JrTools");
            Directory.CreateDirectory(dir);
            return Path.Combine(dir, "selecionador-sistema.json");
        }

        public static async Task<SelecionadorSistemaConfig> LerAsync()
        {
            var path = GetConfigPath();
            if (!File.Exists(path)) return new SelecionadorSistemaConfig();
            try
            {
                var json = await File.ReadAllTextAsync(path);
                return JsonSerializer.Deserialize<SelecionadorSistemaConfig>(json)
                    ?? new SelecionadorSistemaConfig();
            }
            catch
            {
                return new SelecionadorSistemaConfig();
            }
        }

        public static async Task SalvarAsync(SelecionadorSistemaConfig config)
        {
            var path = GetConfigPath();
            var json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(path, json);
        }
    }
}
