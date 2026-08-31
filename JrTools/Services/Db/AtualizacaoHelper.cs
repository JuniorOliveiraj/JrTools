using JrTools.Dto;
using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace JrTools.Services.Db
{
    public static class AtualizacaoHelper
    {
        // Seam de teste: no Windows, Environment.GetFolderPath(SpecialFolder.LocalApplicationData)
        // resolve via API de Known Folder do Shell e ignora SetEnvironmentVariable — não dá pra
        // redirecionar via variável de ambiente em teste. Os testes usam esta propriedade em vez disso.
        internal static string? PastaBaseParaTestes { get; set; }

        private static string GetPath()
        {
            var baseFolder = PastaBaseParaTestes
                ?? Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var folder = Path.Combine(baseFolder, "JrTools");
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
