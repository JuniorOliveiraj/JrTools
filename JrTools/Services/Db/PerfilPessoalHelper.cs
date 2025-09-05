using JrTools.Dto;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Text.Json;

namespace JrTools.Services.Db
{
    public static class PerfilPessoalHelper
    {
        private static string GetConfigPath()
        {
            string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "JrTools");
            Directory.CreateDirectory(folder); 
            return Path.Combine(folder, "DadosPessoais.json");
        }

        public static async Task<DadosPessoaisDataObject> LerConfiguracoesAsync()
        {
            string path = GetConfigPath();

            if (!File.Exists(path))
            {
               
                string assetsPath = Path.Combine(AppContext.BaseDirectory, "Assets", "DadosPessoais.json");
                if (File.Exists(assetsPath))
                {
                    try
                    {
                        File.Copy(assetsPath, path);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Erro ao copiar config do Assets: {ex.Message}");
                        return CriarConfigPadrao();
                    }
                }
                else
                {
                    return CriarConfigPadrao();
                }
            }

            try
            {
                string json = await File.ReadAllTextAsync(path);
                if (string.IsNullOrWhiteSpace(json))
                    return CriarConfigPadrao();

                var config = JsonSerializer.Deserialize<DadosPessoaisDataObject>(json);
                return config ?? CriarConfigPadrao();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao ler o arquivo de config: {ex.Message}");
                return CriarConfigPadrao();
            }
        }

        public static async Task SalvarConfiguracoesAsync(DadosPessoaisDataObject config)
        {
            string path = GetConfigPath();
            try
            {
                string json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(path, json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao salvar o arquivo de config: {ex.Message}");
                
            }
        }

        private static DadosPessoaisDataObject CriarConfigPadrao()
        {
            return new DadosPessoaisDataObject
            {
                LoginDevSite = null,
                SenhaDevSite = null,
                LoginRhWeb = null,
                SenhaRhWeb = null
            };
        }
    }
}
