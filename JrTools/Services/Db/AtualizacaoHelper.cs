using JrTools.Dto;
using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace JrTools.Services.Db
{
    public static class AtualizacaoHelper
    {
        private static string GetPath()
        {
            var folder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "JrTools");
            if (!Directory.Exists(folder))
                Directory.CreateDirectory(folder);
            return Path.Combine(folder, "atualizacao.json");
        }

        public static async Task<AtualizacaoConfigDto> LerAsync()
        {
            var path = GetPath();
            if (!File.Exists(path))
                return new AtualizacaoConfigDto();
            try
            {
                var json = await File.ReadAllTextAsync(path);
                return JsonSerializer.Deserialize<AtualizacaoConfigDto>(json)
                       ?? new AtualizacaoConfigDto();
            }
            catch { return new AtualizacaoConfigDto(); }
        }

        public static async Task SalvarAsync(AtualizacaoConfigDto config)
        {
            var json = JsonSerializer.Serialize(config,
                new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(GetPath(), json);
        }
    }
}
