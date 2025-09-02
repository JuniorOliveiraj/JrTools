using JrTools.Dto;
using System;
using System.IO;
using System.Threading.Tasks;

public static class ConfigHelper
{
    private static string GetConfigPath()
    {
        string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "JrTools");
        if (!Directory.Exists(folder))
            Directory.CreateDirectory(folder);
        return Path.Combine(folder, "config.json");
    }

    public static async Task<ConfiguracoesdataObject> LerConfiguracoesAsync()
    {
        string path = GetConfigPath();

        if (!File.Exists(path))
        {
            // Copiar do Assets
            string assetsPath = Path.Combine(AppContext.BaseDirectory, "Assets", "config.json");
            if (File.Exists(assetsPath))
                File.Copy(assetsPath, path);
            else
                return CriarConfigPadrao();
        }

        try
        {
            string json = await File.ReadAllTextAsync(path);
            if (string.IsNullOrWhiteSpace(json))
                return CriarConfigPadrao();

            var config = System.Text.Json.JsonSerializer.Deserialize<ConfiguracoesdataObject>(json);
            return config ?? CriarConfigPadrao();
        }
        catch
        {
            return CriarConfigPadrao();
        }
    }

    public static async Task SalvarConfiguracoesAsync(ConfiguracoesdataObject config)
    {
        string path = GetConfigPath();
        string json = System.Text.Json.JsonSerializer.Serialize(config, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(path, json);
    }

    private static ConfiguracoesdataObject CriarConfigPadrao()
    {
        return new ConfiguracoesdataObject
        {
            ProjetoSelecionado = "Default",
            DiretorioBinarios = "",
            DiretorioProducao = "",
            DiretorioEspecificos = ""
        };
    }
}
