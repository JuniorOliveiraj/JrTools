using JrTools.Dto;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace JrTools.Services
{
    public class CopyProfileService
    {
        private readonly string _perfisPath;

        public CopyProfileService()
        {
            string folder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "JrTools"
            );

            if (!Directory.Exists(folder))
                Directory.CreateDirectory(folder);

            _perfisPath = Path.Combine(folder, "perfilsCopy.json");
        }

        public async Task<List<PerfilEspelhamento>> CarregarPerfisAsync()
        {
            if (!File.Exists(_perfisPath))
            {
                return new List<PerfilEspelhamento>();
            }

            try
            {
                string json = await File.ReadAllTextAsync(_perfisPath);
                var perfis = JsonSerializer.Deserialize<List<PerfilEspelhamento>>(json);
                return perfis ?? new List<PerfilEspelhamento>();
            }
            catch
            {
                return new List<PerfilEspelhamento>();
            }
        }

        public async Task SalvarPerfisAsync(List<PerfilEspelhamento> perfis)
        {
            try
            {
                var json = JsonSerializer.Serialize(perfis, new JsonSerializerOptions
                {
                    WriteIndented = true
                });

                await File.WriteAllTextAsync(_perfisPath, json);
            }
            catch (Exception ex)
            {
                throw new Exception($"Erro ao salvar perfis: {ex.Message}");
            }
        }
    }
}
