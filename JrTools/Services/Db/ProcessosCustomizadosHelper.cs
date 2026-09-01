using JrTools.Dto;
using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace JrTools.Services.Db
{
    public static class ProcessosCustomizadosHelper
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
            return Path.Combine(folder, "processos-customizados.json");
        }

        public static async Task<ProcessoCustomizadoConfig> LerAsync()
        {
            var path = GetPath();
            if (!File.Exists(path))
                return new ProcessoCustomizadoConfig();
            try
            {
                var json = await File.ReadAllTextAsync(path);
                return JsonSerializer.Deserialize<ProcessoCustomizadoConfig>(json)
                       ?? new ProcessoCustomizadoConfig();
            }
            catch { return new ProcessoCustomizadoConfig(); }
        }

        public static async Task SalvarAsync(ProcessoCustomizadoConfig config)
        {
            var json = JsonSerializer.Serialize(config,
                new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(GetPath(), json);
        }
    }
}
