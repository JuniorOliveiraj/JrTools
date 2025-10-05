using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace JrTools.Services.Db
{
    public static class ProjetosHelper
    {
        private static string GetProjetosPath()
        {
            string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "JrTools");
            if (!Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
            }
            return Path.Combine(folder, "projetos.json");
        }

        public static async Task<List<string>> LerProjetosAsync()
        {
            string path = GetProjetosPath();

            if (!File.Exists(path))
            {
                return new List<string>(); // Retorna uma lista vazia se o arquivo não existir
            }

            try
            {
                string json = await File.ReadAllTextAsync(path);
                if (string.IsNullOrWhiteSpace(json))
                {
                    return new List<string>();
                }

                var projetos = JsonSerializer.Deserialize<List<string>>(json);
                return projetos ?? new List<string>();
            }
            catch
            {
                return new List<string>(); // Retorna lista vazia em caso de erro
            }
        }

        public static async Task SalvarProjetosAsync(IEnumerable<string> projetos)
        {
            string path = GetProjetosPath();
            var options = new JsonSerializerOptions { WriteIndented = true };
            string json = JsonSerializer.Serialize(projetos, options);
            await File.WriteAllTextAsync(path, json);
        }
    }
}